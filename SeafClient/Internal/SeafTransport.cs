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
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly SeafClientOptions _options;

    public SeafTransport(Uri serverUri, SeafClientOptions options)
    {
        if (!serverUri.IsAbsoluteUri) throw new ArgumentException("The server URI must be absolute.", nameof(serverUri));

        // Treat the server URI as a directory so relative API paths resolve under any sub-path (e.g. https://host/seafile/).
        ServerUri = serverUri.AbsoluteUri.EndsWith('/') ? serverUri : new Uri(serverUri.AbsoluteUri + "/");
        _options = options;

        if (options.HttpClient != null)
        {
            _http = options.HttpClient;
        }
        else
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
    public string? Token { get; set; }
    public HttpClient Http => _http;

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

    public async Task<T> GetAsync<T>(string url, CancellationToken ct)
        => SeafJson.Deserialize<T>(await SendForStringAsync(HttpMethod.Get, url, null, ct).ConfigureAwait(false));

    public async Task<T> SendAsync<T>(HttpMethod method, string url, Func<HttpContent>? content, CancellationToken ct)
        => SeafJson.Deserialize<T>(await SendForStringAsync(method, url, content, ct).ConfigureAwait(false));

    public async Task SendAsync(HttpMethod method, string url, Func<HttpContent>? content, CancellationToken ct)
        => await SendForStringAsync(method, url, content, ct).ConfigureAwait(false);

    public async Task<string> SendForStringAsync(HttpMethod method, string url, Func<HttpContent>? content, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, url, content, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    public static Func<HttpContent> Form(params (string Key, string? Value)[] fields) => () =>
        new FormUrlEncodedContent(fields.Where(f => f.Value != null).Select(f => new KeyValuePair<string, string>(f.Key, f.Value!)));

    public static Func<HttpContent> Json(object body) => () =>
        new StringContent(JsonSerializer.Serialize(body, SeafJson.Options), Encoding.UTF8, "application/json");

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
        var uri = Resolve(url);
        var idempotent = method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Put || method == HttpMethod.Delete;

        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);
            if (authenticate && Token != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Token", Token);
            if (content != null)
                request.Content = content();
            configure?.Invoke(request);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, completion, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (idempotent && attempt < _options.MaxRetries)
            {
                await Task.Delay(Backoff(attempt, null), ct).ConfigureAwait(false);
                continue;
            }

            if (response.IsSuccessStatusCode)
                return response;

            var status = response.StatusCode;
            var retryAfter = GetRetryAfter(response);
            var retryable = status == HttpStatusCode.TooManyRequests
                || (idempotent && status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout);

            if (retryable && attempt < _options.MaxRetries)
            {
                response.Dispose();
                await Task.Delay(Backoff(attempt, retryAfter), ct).ConfigureAwait(false);
                continue;
            }

            string body;
            try { body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false); }
            catch { body = ""; }
            finally { response.Dispose(); }

            var message = ExtractErrorMessage(body) ?? response.ReasonPhrase ?? status.ToString();
            if (status == HttpStatusCode.TooManyRequests)
                throw new SeafRateLimitException(message, retryAfter, body, method, uri);
            throw new SeafException(status, message, body, method, uri);
        }
    }

    private TimeSpan Backoff(int attempt, TimeSpan? retryAfter)
    {
        var delay = retryAfter ?? TimeSpan.FromMilliseconds(_options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt));
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
                foreach (var key in new[] { "error_msg", "detail", "error", "non_field_errors", "message" })
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
        if (_ownsHttp) _http.Dispose();
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
