using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SeafClient.Internal;

/// <summary>
/// HTTP plumbing shared by every API group: URL building, auth header, error mapping and retries.
/// </summary>
internal sealed class SeafTransport : IDisposable
{
    private static readonly MediaTypeWithQualityHeaderValue AcceptJson = new("application/json");
    private static readonly MediaTypeHeaderValue JsonMediaType = new("application/json", "utf-8");
    private static readonly string[] ErrorKeys = ["error_msg", "detail", "error", "non_field_errors", "message"];

    private readonly HttpClient? _http;
    private readonly Func<HttpClient>? _httpFactory;
    private readonly bool _ownsHttp;
    private readonly SeafClientOptions _options;
    private readonly Func<CancellationToken, Task<string>>? _tokenProvider;
    private readonly SemaphoreSlim? _tokenLock;
    private volatile string? _token;
    private volatile bool _disposed;

    public SeafTransport(Uri serverUri, SeafClientOptions options, Func<CancellationToken, Task<string>>? tokenProvider = null)
    {
        if (!serverUri.IsAbsoluteUri) throw new ArgumentException("The server URI must be absolute.", nameof(serverUri));
        if (options.HttpClient != null && options.HttpClientFactory != null)
            throw new ArgumentException("Specify either HttpClient or HttpClientFactory, not both.", nameof(options));

        // Treat the server URI as a directory so relative API paths resolve under any sub-path (e.g. https://host/seafile/).
        ServerUri = serverUri.AbsoluteUri.EndsWith('/') ? serverUri : new Uri(serverUri.AbsoluteUri + "/");
        _options = options;
        _httpFactory = options.HttpClientFactory;
        _tokenProvider = tokenProvider;
        _tokenLock = tokenProvider != null ? new SemaphoreSlim(1, 1) : null;

        if (options.HttpClient != null)
        {
            _http = options.HttpClient;
        }
        else if (_httpFactory == null)
        {
            _http = new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            { Timeout = options.Timeout };
            _ownsHttp = true;
        }
    }

    public Uri ServerUri { get; }
    public string? Token { get => _token; set => _token = value; }

    public Uri Resolve(string relativeOrAbsolute)
    {
        if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var abs) && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            return RewriteFileServerUri(abs);
        return new Uri(ServerUri, relativeOrAbsolute.TrimStart('/'));
    }

    /// <summary>Applies <see cref="SeafClientOptions.RewriteFileServerUrls"/> to a server-issued absolute URL.</summary>
    public Uri RewriteFileServerUri(Uri uri)
    {
        if (!_options.RewriteFileServerUrls) return uri;
        return new UriBuilder(uri) { Scheme = ServerUri.Scheme, Host = ServerUri.Host, Port = ServerUri.Port }.Uri;
    }

    // ---------- typed helpers ----------

    public Task<T> GetAsync<T>(string url, CancellationToken ct)
        => SendAsync<T>(HttpMethod.Get, url, null, ct);

    public async Task<T> SendAsync<T>(HttpMethod method, string url, Func<HttpContent>? content, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, url, content, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await SeafJson.DeserializeAsync<T>(stream, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the response content stream and disposes <paramref name="response"/> if obtaining the stream fails.
    /// When returned, disposing the stream releases the underlying HTTP response/connection in SocketsHttpHandler.
    /// </summary>
    public static async Task<Stream> ReadStreamAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken ct)
    {
        using var response = await SendRawAsync(HttpMethod.Get, url, null, HttpCompletionOption.ResponseHeadersRead, ct, authenticate: false).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        if (response.Content.Headers.ContentLength is long contentLength and >= 0 && contentLength <= Array.MaxLength)
        {
            if (contentLength == 0) return [];
            var buffer = GC.AllocateUninitializedArray<byte>((int)contentLength);
            var read = await ReadUpToAsync(stream, buffer, ct).ConfigureAwait(false);
            return read == buffer.Length ? buffer : buffer[..read];
        }

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    public async Task<string> DownloadTextAsync(string url, Encoding? encoding, CancellationToken ct)
    {
        var enc = encoding ?? Encoding.UTF8;
        using var response = await SendRawAsync(HttpMethod.Get, url, null, HttpCompletionOption.ResponseHeadersRead, ct, authenticate: false).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        if (response.Content.Headers.ContentLength is long contentLength and >= 0 && contentLength <= 16 * 1024 * 1024)
        {
            if (contentLength == 0) return string.Empty;
            var len = (int)contentLength;
            var rented = ArrayPool<byte>.Shared.Rent(len);
            try
            {
                var read = await ReadUpToAsync(stream, rented.AsMemory(0, len), ct).ConfigureAwait(false);
                return enc.GetString(rented.AsSpan(0, read));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        using var reader = new StreamReader(stream, enc, detectEncodingFromByteOrderMarks: false);
        return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
    }

    private static async Task<int> ReadUpToAsync(Stream stream, Memory<byte> destination, CancellationToken ct)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var n = await stream.ReadAsync(destination[offset..], ct).ConfigureAwait(false);
            if (n == 0) break;
            offset += n;
        }
        return offset;
    }

    public async Task SendAsync(HttpMethod method, string url, Func<HttpContent>? content, CancellationToken ct)
        => await SendForStringAsync(method, url, content, ct).ConfigureAwait(false);

    public async Task<string> SendForStringAsync(HttpMethod method, string url, Func<HttpContent>? content, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, url, content, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    public static Func<HttpContent> Form(params (string Key, string? Value)[] fields) => () =>
        new FormUrlEncodedContent(fields.Where(f => f.Value != null).Select(f => new KeyValuePair<string, string>(f.Key, f.Value!)));

    public static Func<HttpContent> Json<T>(T body) => () =>
        new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, SeafJson.Options))
        {
            Headers = { ContentType = JsonMediaType },
        };

    /// <summary>
    /// Creates a <see cref="MultipartFormDataContent"/> with UTF-8 header encoding and an unquoted boundary.
    /// .NET quotes multipart boundaries by default, which Seafile 10's file server rejects with 400 Bad Request.
    /// </summary>
    public static MultipartFormDataContent CreateMultipart()
    {
        var boundary = "----SeafClient" + Guid.NewGuid().ToString("N");
        var form = new MultipartFormDataContent(boundary)
        {
            // Seafile's file server reads raw UTF-8 names; .NET's default RFC 2047 encoding of non-ASCII names is not understood.
            HeaderEncodingSelector = (_, _) => Encoding.UTF8,
        };
        // Replace .NET's default quoted boundary parameter (boundary="...") with an unquoted token.
        form.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data")
        {
            Parameters = { new NameValueHeaderValue("boundary", boundary) },
        };
        return form;
    }

    // ---------- core send with retries ----------

    /// <summary>
    /// Sends a request and returns the successful response (caller disposes). Non-success responses are mapped to
    /// <see cref="SeafException"/>. <paramref name="content"/> is a factory so the body can be rebuilt on retry.
    /// </summary>
    public async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, Func<HttpContent>? content, HttpCompletionOption completion, CancellationToken ct, bool authenticate = true, Action<HttpRequestMessage>? configure = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var uri = Resolve(url);
        var idempotent = method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Put || method == HttpMethod.Delete;
        var activeToken = authenticate ? await EnsureTokenAsync(ct).ConfigureAwait(false) : null;
        var authRefreshed = false;

        for (var attempt = 0; ; )
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Accept.Add(AcceptJson);
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);
            if (authenticate && activeToken != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Token", activeToken);
            if (content != null)
                request.Content = content();
            configure?.Invoke(request);

            var client = _httpFactory?.Invoke() ?? _http!;
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, completion, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (idempotent && attempt < _options.MaxRetries)
                {
                    await Task.Delay(Backoff(attempt++, null), ct).ConfigureAwait(false);
                    continue;
                }
                throw new SeafConnectionException(ex.Message, method, uri, ex);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                if (idempotent && attempt < _options.MaxRetries)
                {
                    await Task.Delay(Backoff(attempt++, null), ct).ConfigureAwait(false);
                    continue;
                }
                throw new SeafTimeoutException(method, uri, ex);
            }

            if (response.IsSuccessStatusCode)
                return response;

            var status = response.StatusCode;
            if (status == HttpStatusCode.Unauthorized && authenticate && _tokenProvider != null && !authRefreshed)
            {
                response.Dispose();
                activeToken = await RefreshTokenAsync(activeToken, ct).ConfigureAwait(false);
                authRefreshed = true;
                continue;
            }

            var retryAfter = GetRetryAfter(response);
            var retryable = status == HttpStatusCode.TooManyRequests
                || (idempotent && status is HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout);

            if (retryable && attempt < _options.MaxRetries)
            {
                response.Dispose();
                await Task.Delay(Backoff(attempt++, retryAfter), ct).ConfigureAwait(false);
                continue;
            }

            string body;
            try { body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false); }
            catch (Exception) when (!ct.IsCancellationRequested) { body = ""; }
            finally { response.Dispose(); }

            var message = ExtractErrorMessage(body) ?? response.ReasonPhrase ?? status.ToString();
            if (status == HttpStatusCode.TooManyRequests)
                throw new SeafRateLimitException(message, retryAfter, body, method, uri);
            throw new SeafException(status, message, body, method, uri);
        }
    }

    private async Task<string?> EnsureTokenAsync(CancellationToken ct)
    {
        if (_token is { } tok) return tok;
        if (_tokenProvider == null) return null;

        await _tokenLock!.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _token ??= await _tokenProvider(ct).ConfigureAwait(false);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<string> RefreshTokenAsync(string? failedToken, CancellationToken ct)
    {
        await _tokenLock!.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_token is { } current && current != failedToken)
                return current;
            return _token = await _tokenProvider!(ct).ConfigureAwait(false);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private TimeSpan Backoff(int attempt, TimeSpan? retryAfter)
    {
        TimeSpan delay;
        if (retryAfter is { } ra)
        {
            delay = ra;
        }
        else
        {
            var baseMs = _options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt);
            var jitter = 0.8 + Random.Shared.NextDouble() * 0.4; // +/- 20%
            delay = TimeSpan.FromMilliseconds(baseMs * jitter);
        }
        if (delay < TimeSpan.Zero) return TimeSpan.Zero;
        return delay > _options.MaxRetryDelay ? _options.MaxRetryDelay : delay;
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var ra = response.Headers.RetryAfter;
        if (ra == null) return null;
        if (ra.Delta is { } d) return d;
        if (ra.Date is { } date) { var diff = date - DateTimeOffset.UtcNow; return diff > TimeSpan.Zero ? diff : TimeSpan.Zero; }
        return null;
    }

    /// <summary>
    /// Seafile reports errors in several shapes: <c>{"error_msg": "..."}</c>, <c>{"detail": "..."}</c>,
    /// <c>{"error": "..."}</c>, <c>{"non_field_errors": ["..."]}</c>, or a bare JSON string / HTML page.
    /// </summary>
    internal static string? ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String) return root.GetString();
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in ErrorKeys)
                    if (root.TryGetProperty(key, out var v)) return Flatten(v);

                // Field validation errors: {"email": ["Enter a valid email address."]}
                foreach (var prop in root.EnumerateObject())
                    return $"{prop.Name}: {Flatten(prop.Value)}";
            }
            return Flatten(root);
        }
        catch (JsonException)
        {
            var text = body.Trim();
            if (text.StartsWith('<'))
            {
                var start = text.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
                var end = text.IndexOf("</title>", StringComparison.OrdinalIgnoreCase);
                if (start >= 0 && end > start) return text[(start + 7)..end].Trim();
            }
            return text.Length > 300 ? text[..300] : text;
        }
    }

    private static string Flatten(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Array => string.Join("; ", v.EnumerateArray().Select(Flatten)),
        _ => v.GetRawText(),
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsHttp) _http?.Dispose();
        _tokenLock?.Dispose();
    }
}

/// <summary>Small fluent query-string builder; values are URI-escaped and nulls are skipped.</summary>
internal sealed class Url
{
    private readonly StringBuilder _sb;
    private bool _hasQuery;

    private Url(string path) { _sb = new StringBuilder(path); _hasQuery = path.Contains('?'); }

    public static Url Of(string path) => new(path);

    public Url Q(string key, string? value)
    {
        if (value == null) return this;
        _sb.Append(_hasQuery ? '&' : '?').Append(key).Append('=').Append(Uri.EscapeDataString(value));
        _hasQuery = true;
        return this;
    }

    public Url Q(string key, bool? value) => value == null ? this : Q(key, value.Value ? "true" : "false");
    public Url Q(string key, int? value) => value == null ? this : Q(key, value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public override string ToString() => _sb.ToString();
    public static implicit operator string(Url u) => u.ToString();

    /// <summary>Escapes a single path segment (e.g. an email address or token).</summary>
    public static string Seg(string value) => Uri.EscapeDataString(value);
}
