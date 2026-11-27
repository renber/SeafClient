using System.Net;

namespace SeafClient;

/// <summary>
/// Thrown when the Seafile server returns a non-success response (or a request fails a client-side precondition).
/// </summary>
public class SeafException : Exception
{
    public SeafException(HttpStatusCode statusCode, string message, string? responseBody = null, HttpMethod? method = null, Uri? requestUri = null, Exception? innerException = null)
        : base(BuildMessage(statusCode, message, method, requestUri), innerException)
    {
        StatusCode = statusCode;
        ServerMessage = message;
        ResponseBody = responseBody;
        Method = method;
        RequestUri = requestUri;
    }

    /// <summary>HTTP status code returned by the server (0 when no HTTP response was received).</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The error message extracted from the server response (<c>error_msg</c>, <c>detail</c>, ...).</summary>
    public string ServerMessage { get; }

    /// <summary>Raw response body, if any.</summary>
    public string? ResponseBody { get; }

    public HttpMethod? Method { get; }

    /// <summary>The request URI. Query strings are kept, but never contain credentials.</summary>
    public Uri? RequestUri { get; }

    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
    public bool IsBadRequest => StatusCode == HttpStatusCode.BadRequest;
    public bool IsConflict => StatusCode == HttpStatusCode.Conflict;
    public bool IsRateLimited => StatusCode == HttpStatusCode.TooManyRequests;
    public bool IsServerError => (int)StatusCode >= 500;

    /// <summary>True for errors that may succeed on a later retry (network errors, timeouts, 408, 429, 502/503/504).</summary>
    public bool IsTransient => this is SeafConnectionException or SeafTimeoutException or SeafRateLimitException
        || StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static string BuildMessage(HttpStatusCode code, string message, HttpMethod? method, Uri? uri)
    {
        var where = method != null && uri != null ? $" ({method} {uri.AbsolutePath})" : "";
        return code == 0
            ? $"Seafile request failed: {message}{where}"
            : $"Seafile returned {(int)code} {code}: {message}{where}";
    }
}

/// <summary>
/// Thrown when the server keeps answering 429 Too Many Requests after all retries were exhausted.
/// </summary>
public sealed class SeafRateLimitException : SeafException
{
    public SeafRateLimitException(string message, TimeSpan? retryAfter, string? responseBody, HttpMethod? method, Uri? requestUri)
        : base(HttpStatusCode.TooManyRequests, message, responseBody, method, requestUri)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>How long the server asked the client to wait, if it said so.</summary>
    public TimeSpan? RetryAfter { get; }
}

/// <summary>
/// Thrown when the Seafile server cannot be reached (DNS, connection refused, TLS, or network drop).
/// </summary>
public sealed class SeafConnectionException : SeafException
{
    public SeafConnectionException(string message, HttpMethod? method, Uri? requestUri, Exception? innerException = null)
        : base(0, message, null, method, requestUri, innerException)
    {
    }
}

/// <summary>
/// Thrown when a Seafile HTTP request times out before the caller's <see cref="CancellationToken"/> is canceled.
/// </summary>
public sealed class SeafTimeoutException : SeafException
{
    public SeafTimeoutException(HttpMethod? method, Uri? requestUri, Exception? innerException = null)
        : base(HttpStatusCode.RequestTimeout, "The request to Seafile timed out.", null, method, requestUri, innerException)
    {
    }
}
