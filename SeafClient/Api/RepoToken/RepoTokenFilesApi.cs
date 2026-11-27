using System.Net;
using System.Text;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api.RepoToken;

/// <summary>Files of a repo-token library (<c>/api/v2.1/via-repo-token/file/</c>, upload and download links).</summary>
public sealed class RepoTokenFilesApi
{
    private readonly RepoTokenContext _c;
    internal RepoTokenFilesApi(RepoTokenContext c) => _c = c;

    private const string FileUrl = RepoTokenContext.Base + "file/";

    /// <summary>Gets file details. Throws a 404 <see cref="SeafException"/> if the file does not exist.</summary>
    public async Task<SeafFile> GetAsync(string path, CancellationToken ct = default)
        => (await _c.T.GetAsync<SeafOperationResult>(Url.Of(FileUrl).Q("path", SeafPath.Normalize(path)), ct).ConfigureAwait(false)).ToFile();

    /// <summary>Gets file details, or null if the file does not exist.</summary>
    public async Task<SeafFile?> TryGetAsync(string path, CancellationToken ct = default)
    {
        try { return await GetAsync(path, ct).ConfigureAwait(false); }
        catch (SeafException e) when (e.IsNotFound) { return null; }
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken ct = default)
        => await TryGetAsync(path, ct).ConfigureAwait(false) != null;

    // ---------------- download ----------------

    /// <summary>Returns a download URL for the file (no authentication needed to fetch it).</summary>
    public async Task<string> GetDownloadLinkAsync(string path, CancellationToken ct = default)
    {
        var link = await _c.T.GetAsync<string>(Url.Of(RepoTokenContext.Base + "download-link/").Q("path", SeafPath.Normalize(path)), ct).ConfigureAwait(false);
        return _c.T.RewriteFileServerUri(new Uri(link)).AbsoluteUri;
    }

    /// <summary>Opens a read stream for a file. Dispose the stream when done.</summary>
    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var link = await GetDownloadLinkAsync(path, ct).ConfigureAwait(false);
        var response = await _c.T.SendRawAsync(HttpMethod.Get, link, null, HttpCompletionOption.ResponseHeadersRead, ct, authenticate: false).ConfigureAwait(false);
        return await SeafTransport.ReadStreamAsync(response, ct).ConfigureAwait(false);
    }

    public async Task DownloadAsync(string path, Stream destination, CancellationToken ct = default)
    {
        await using var source = await OpenReadAsync(path, ct).ConfigureAwait(false);
        await source.CopyToAsync(destination, ct).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default)
        => await _c.T.DownloadBytesAsync(await GetDownloadLinkAsync(path, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>Downloads a text file, decoding as UTF-8 by default (the file server's declared charset is unreliable).</summary>
    public async Task<string> ReadAllTextAsync(string path, Encoding? encoding = null, CancellationToken ct = default)
        => await _c.T.DownloadTextAsync(await GetDownloadLinkAsync(path, ct).ConfigureAwait(false), encoding, ct).ConfigureAwait(false);

    // ---------------- upload ----------------

    /// <summary>Returns a fresh upload URL for <paramref name="parentDir"/> (which must exist). Requires an <c>rw</c> token.</summary>
    public async Task<string> GetUploadLinkAsync(string parentDir = "/", CancellationToken ct = default)
    {
        var link = await _c.T.GetAsync<string>(Url.Of(RepoTokenContext.Base + "upload-link/").Q("path", SeafPath.Normalize(parentDir)), ct).ConfigureAwait(false);
        return _c.T.RewriteFileServerUri(new Uri(link)).AbsoluteUri;
    }

    /// <summary>
    /// Uploads a file into <paramref name="parentDir"/>. Unless <see cref="SeafUploadOptions.Replace"/> is set, an existing
    /// file is kept and the upload is stored under a new name; check <see cref="SeafUploadedFile.Name"/>.
    /// </summary>
    public async Task<SeafUploadedFile> UploadAsync(string parentDir, string fileName, Stream content, SeafUploadOptions? options = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        if (fileName.Contains('/') || fileName.Contains('\\')) throw new ArgumentException("File name must not contain '/' or '\\'.", nameof(fileName));
        ArgumentNullException.ThrowIfNull(content);
        options ??= new SeafUploadOptions();
        parentDir = SeafPath.Normalize(parentDir);

        var linkDir = options.CreateParents ? SeafPath.Root : parentDir;
        var link = await GetUploadLinkAsync(linkDir, ct).ConfigureAwait(false);

        var fields = new List<(string, string)> { ("parent_dir", linkDir) };
        if (options.CreateParents && parentDir != SeafPath.Root) fields.Add(("relative_path", parentDir.TrimStart('/')));
        if (options.Replace) fields.Add(("replace", "1"));

        var json = await _c.T.SendForStringAsync(HttpMethod.Post, Url.Of(link).Q("ret-json", "1"),
            FilesApi.BuildReplayableUpload(fields, fileName, content, options.ContentType, options.Progress), ct).ConfigureAwait(false);

        var uploaded = SeafJson.Deserialize<List<SeafUploadedFile>>(json).FirstOrDefault()
            ?? throw new SeafException(HttpStatusCode.InternalServerError, "Upload returned no file information.", json);
        var libraryId = await _c.GetLibraryIdAsync(ct).ConfigureAwait(false);
        return uploaded with { LibraryId = libraryId, Path = SeafPath.Combine(parentDir, uploaded.Name) };
    }

    public async Task<SeafUploadedFile> UploadFileAsync(string parentDir, string localFilePath, SeafUploadOptions? options = null, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(localFilePath);
        return await UploadAsync(parentDir, Path.GetFileName(localFilePath), fs, options, ct).ConfigureAwait(false);
    }

    // ---------------- mutations ----------------

    /// <summary>Creates an empty file. Seafile auto-renames if the name is taken; the returned path is authoritative.</summary>
    public async Task<SeafFile> CreateAsync(string path, CancellationToken ct = default)
        => (await Operation(path, ct, ("operation", "create")).ConfigureAwait(false)).ToFile();

    /// <summary>Renames a file. If the new name is taken, Seafile picks a free one; the returned path is authoritative.</summary>
    public async Task<SeafFile> RenameAsync(string path, string newName, CancellationToken ct = default)
        => (await Operation(path, ct, ("operation", "rename"), ("newname", newName)).ConfigureAwait(false)).ToFile();

    /// <summary>Copies a file into <paramref name="destinationDir"/> (same library). Name clashes produce <c>name (1)</c>.</summary>
    public async Task<SeafFile> CopyAsync(string path, string destinationDir, CancellationToken ct = default)
        => (await Operation(path, ct, ("operation", "copy"), ("dst_dir", SeafPath.Normalize(destinationDir))).ConfigureAwait(false)).ToFile();

    /// <summary>Moves a file into <paramref name="destinationDir"/> (same library). Name clashes produce <c>name (1)</c>.</summary>
    public async Task<SeafFile> MoveAsync(string path, string destinationDir, CancellationToken ct = default)
        => (await Operation(path, ct, ("operation", "move"), ("dst_dir", SeafPath.Normalize(destinationDir))).ConfigureAwait(false)).ToFile();

    /// <summary>Restores a file to its content in commit <paramref name="commitId"/> (see the file history).</summary>
    public Task RevertAsync(string path, string commitId, CancellationToken ct = default)
        => _c.T.SendAsync(HttpMethod.Post, Url.Of(FileUrl).Q("path", SeafPath.Normalize(path)),
            SeafTransport.Form(("operation", "revert"), ("commit_id", commitId)), ct);

    /// <summary>Locks a file (Pro). <paramref name="expireSeconds"/> limits the lock's lifetime; null locks indefinitely.</summary>
    public async Task<SeafFile> LockAsync(string path, int? expireSeconds = null, CancellationToken ct = default)
        => (await _c.T.SendAsync<SeafOperationResult>(HttpMethod.Put, Url.Of(FileUrl).Q("path", SeafPath.Normalize(path)),
            SeafTransport.Form(("operation", "lock"), ("expire", expireSeconds?.ToString(System.Globalization.CultureInfo.InvariantCulture))), ct).ConfigureAwait(false)).ToFile();

    public async Task<SeafFile> UnlockAsync(string path, CancellationToken ct = default)
        => (await _c.T.SendAsync<SeafOperationResult>(HttpMethod.Put, Url.Of(FileUrl).Q("path", SeafPath.Normalize(path)),
            SeafTransport.Form(("operation", "unlock")), ct).ConfigureAwait(false)).ToFile();

    /// <summary>Deletes a file. Succeeds silently if the file does not exist.</summary>
    public Task DeleteAsync(string path, CancellationToken ct = default)
        => _c.T.SendAsync(HttpMethod.Delete, Url.Of(FileUrl).Q("path", SeafPath.Normalize(path)), null, ct);

    private Task<SeafOperationResult> Operation(string path, CancellationToken ct, params (string, string?)[] form)
        => _c.T.SendAsync<SeafOperationResult>(HttpMethod.Post, Url.Of(FileUrl).Q("path", SeafPath.Normalize(path)), SeafTransport.Form(form), ct);
}

/// <summary>Batch operations on several entries of one directory of a repo-token library.</summary>
public sealed class RepoTokenItemsApi
{
    private readonly RepoTokenContext _c;
    internal RepoTokenItemsApi(RepoTokenContext c) => _c = c;

    /// <summary>Copies entries of <paramref name="sourceDir"/> into <paramref name="destinationDir"/> (synchronously).</summary>
    public Task CopyAsync(string sourceDir, IEnumerable<string> names, string destinationDir, CancellationToken ct = default)
        => Batch("sync-batch-copy-item/", sourceDir, names, destinationDir, ct);

    /// <summary>Moves entries of <paramref name="sourceDir"/> into <paramref name="destinationDir"/> (synchronously).</summary>
    public Task MoveAsync(string sourceDir, IEnumerable<string> names, string destinationDir, CancellationToken ct = default)
        => Batch("sync-batch-move-item/", sourceDir, names, destinationDir, ct);

    /// <summary>Deletes entries of <paramref name="parentDir"/>.</summary>
    public Task DeleteAsync(string parentDir, IEnumerable<string> names, CancellationToken ct = default)
    {
        var list = names.ToArray();
        if (list.Length == 0) return Task.CompletedTask;
        return _c.T.SendAsync(HttpMethod.Delete, RepoTokenContext.Base + "batch-delete-item/", SeafTransport.Json(new
        {
            parent_dir = SeafPath.Normalize(parentDir),
            dirents = list,
        }), ct);
    }

    private Task Batch(string endpoint, string srcDir, IEnumerable<string> names, string dstDir, CancellationToken ct)
    {
        var list = names.ToArray();
        if (list.Length == 0) return Task.CompletedTask;
        return _c.T.SendAsync(HttpMethod.Post, RepoTokenContext.Base + endpoint, SeafTransport.Json(new
        {
            src_parent_dir = SeafPath.Normalize(srcDir),
            src_dirents = list,
            dst_parent_dir = SeafPath.Normalize(dstDir),
        }), ct);
    }
}

/// <summary>Share links created through a repo API token.</summary>
public sealed class RepoTokenShareLinksApi
{
    private readonly RepoTokenContext _c;
    internal RepoTokenShareLinksApi(RepoTokenContext c) => _c = c;

    /// <summary>
    /// Creates a share link for a file or directory (or the whole library with <c>/</c>), owned by the user who generated
    /// the token. Passwords and expiry are not supported on this API. Requires an <c>rw</c> token.
    /// </summary>
    public Task<SeafShareLink> CreateAsync(string path, SeafShareLinkPermissions? permissions = null, CancellationToken ct = default)
    {
        permissions ??= new SeafShareLinkPermissions();
        return _c.T.SendAsync<SeafShareLink>(HttpMethod.Post, RepoTokenContext.Base + "share-links/", SeafTransport.Json(new
        {
            path = SeafPath.Normalize(path),
            permissions = new { can_edit = permissions.CanEdit, can_download = permissions.CanDownload, can_upload = permissions.CanUpload },
        }), ct);
    }
}
