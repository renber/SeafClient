using System.Globalization;
using System.Net;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>Public upload links: let people without an account upload files into a folder.</summary>
public sealed partial class UploadLinksApi
{
    private readonly SeafTransport _t;
    internal UploadLinksApi(SeafTransport t) => _t = t;

    /// <summary>
    /// Lists the current user's upload links, optionally filtered to a library and a folder in it
    /// (the folder must exist; a trailing '/' is optional).
    /// </summary>
    public async Task<IReadOnlyList<SeafUploadLink>> ListAsync(string? libraryId = null, string? path = null, CancellationToken ct = default)
    {
        if (path != null && libraryId == null) throw new ArgumentException("A path filter requires a library id.", nameof(path));
        return await _t.GetAsync<List<SeafUploadLink>>(Url.Of("api/v2.1/upload-links/")
            .Q("repo_id", libraryId).Q("path", path == null ? null : SeafPath.Normalize(path)), ct).ConfigureAwait(false);
    }

    public Task<SeafUploadLink> GetAsync(string token, CancellationToken ct = default)
        => _t.GetAsync<SeafUploadLink>($"api/v2.1/upload-links/{Url.Seg(token)}/", ct);

    /// <summary>Creates an upload link for a folder.</summary>
    /// <remarks>
    /// Seafile keeps one upload link per user and folder: if one already exists it is returned unchanged and
    /// <paramref name="options"/> are ignored. Delete it first, or use <see cref="UpdateAsync"/>, to change it.
    /// </remarks>
    public Task<SeafUploadLink> CreateAsync(string libraryId, string path, SeafUploadLinkOptions? options = null, CancellationToken ct = default)
    {
        options ??= new SeafUploadLinkOptions();
        return _t.SendAsync<SeafUploadLink>(HttpMethod.Post, "api/v2.1/upload-links/", SeafTransport.Json(new
        {
            repo_id = libraryId,
            path = SeafPath.Normalize(path),
            password = options.Password,
            expiration_time = Iso(options.ExpiresAt),
            expire_days = options.ExpiresAt == null ? options.ExpireDays : null,
        }), ct);
    }

    /// <summary>Changes the expiry of an upload link (the only property Seafile lets you change).</summary>
    public Task<SeafUploadLink> UpdateAsync(string token, DateTimeOffset? expiresAt = null, int? expireDays = null, CancellationToken ct = default)
    {
        if (expiresAt == null && expireDays == null) throw new ArgumentException("Pass expiresAt or expireDays.");
        return _t.SendAsync<SeafUploadLink>(HttpMethod.Put, $"api/v2.1/upload-links/{Url.Seg(token)}/", SeafTransport.Json(new
        {
            expiration_time = Iso(expiresAt),
            expire_days = expiresAt == null ? expireDays : null,
        }), ct);
    }

    public Task DeleteAsync(string token, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, $"api/v2.1/upload-links/{Url.Seg(token)}/", null, ct);

    /// <summary>Deletes several upload links. Unknown tokens are ignored.</summary>
    public Task DeleteManyAsync(IEnumerable<string> tokens, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, "api/v2.1/upload-links/", SeafTransport.Json(new { tokens = tokens.ToList() }), ct);

    /// <summary>Emails the link to one or more addresses. Fails with 403 if the server has no email service configured.</summary>
    public async Task<SeafSendLinkResult> SendAsync(string token, IEnumerable<string> emails, string? extraMessage = null, CancellationToken ct = default)
        => await _t.SendAsync<SeafSendLinkResult>(HttpMethod.Post, "api2/send-upload-link/", SeafTransport.Form(
            ("token", token), ("email", string.Join(',', emails)), ("extra_msg", extraMessage)), ct).ConfigureAwait(false);

    /// <summary>
    /// Returns a file-server URL that accepts uploads into the link's folder.
    /// </summary>
    /// <remarks>
    /// Fails with 403 for password-protected links: Seafile only hands out the URL after the password was entered in a
    /// browser session.
    /// </remarks>
    public async Task<string> GetUploadUrlAsync(string token, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<UploadUrlResponse>($"api/v2.1/upload-links/{Url.Seg(token)}/upload/", ct).ConfigureAwait(false);
        return _t.RewriteFileServerUri(new Uri(r.UploadLink)).AbsoluteUri;
    }

    /// <summary>
    /// Uploads a file through an upload link, as an anonymous visitor would. Only <see cref="SeafUploadOptions.Replace"/>,
    /// <see cref="SeafUploadOptions.Progress"/> and <see cref="SeafUploadOptions.ContentType"/> apply.
    /// </summary>
    public async Task<SeafUploadedFile> UploadAsync(string token, string fileName, Stream content, SeafUploadOptions? options = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        if (fileName.Contains('/') || fileName.Contains('\\')) throw new ArgumentException("File name must not contain '/' or '\\'.", nameof(fileName));
        ArgumentNullException.ThrowIfNull(content);
        options ??= new SeafUploadOptions();

        var linkTask = GetAsync(token, ct);
        var url = await GetUploadUrlAsync(token, ct).ConfigureAwait(false);
        var link = await linkTask.ConfigureAwait(false);

        // The file server only accepts the link's own folder as parent_dir ("Parent dir doesn't match." otherwise).
        var parentDir = SeafPath.Normalize(link.Path);
        var fields = new List<(string, string)> { ("parent_dir", parentDir) };
        if (options.Replace) fields.Add(("replace", "1"));

        var json = await _t.SendForStringAsync(HttpMethod.Post, Url.Of(url).Q("ret-json", "1"),
            FilesApi.BuildUploadBody(fields, fileName, content, options), ct).ConfigureAwait(false);
        var uploaded = SeafJson.Deserialize<List<SeafUploadedFile>>(json).FirstOrDefault()
            ?? throw new SeafException(HttpStatusCode.InternalServerError, "Upload returned no file information.", json);
        return uploaded with { LibraryId = link.LibraryId, Path = SeafPath.Combine(parentDir, uploaded.Name) };
    }

    private static string? Iso(DateTimeOffset? d) => d?.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    private sealed record UploadUrlResponse { public string UploadLink { get; init; } = ""; }
}
