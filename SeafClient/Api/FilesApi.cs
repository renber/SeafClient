using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>File endpoints, including upload and download.</summary>
public sealed partial class FilesApi
{
    private readonly SeafTransport _t;
    internal FilesApi(SeafTransport t) => _t = t;

    private static string FileUrl(string libraryId) => $"api/v2.1/repos/{Url.Seg(libraryId)}/file/";

    // ---------------- metadata ----------------

    /// <summary>Gets file details. Throws a 404 <see cref="SeafException"/> if the file does not exist.</summary>
    public async Task<SeafFile> GetAsync(string libraryId, string path, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        var file = await _t.GetAsync<SeafFile>(Url.Of($"api2/repos/{Url.Seg(libraryId)}/file/detail/").Q("p", path), ct).ConfigureAwait(false);
        return file with { LibraryId = libraryId, Path = path };
    }

    /// <summary>Gets file details, or null if the file does not exist.</summary>
    public async Task<SeafFile?> TryGetAsync(string libraryId, string path, CancellationToken ct = default)
    {
        try { return await GetAsync(libraryId, path, ct).ConfigureAwait(false); }
        catch (SeafException e) when (e.IsNotFound) { return null; }
    }

    public async Task<bool> ExistsAsync(string libraryId, string path, CancellationToken ct = default)
        => await TryGetAsync(libraryId, path, ct).ConfigureAwait(false) != null;

    /// <summary>Returns the change history of a file, newest first.</summary>
    /// <remarks>History is indexed asynchronously by the server; a change may take a few seconds to appear.</remarks>
    public async Task<IReadOnlyList<SeafFileRevision>> GetHistoryAsync(string libraryId, string path, int page = 1, int perPage = 25, CancellationToken ct = default)
    {
        var result = await _t.GetAsync<HistoryResponse>(Url.Of($"api/v2.1/repos/{Url.Seg(libraryId)}/file/new_history/")
            .Q("path", SeafPath.Normalize(path)).Q("page", page).Q("per_page", perPage), ct).ConfigureAwait(false);
        return result.Data;
    }

    // ---------------- download ----------------

    /// <summary>
    /// Returns a temporary download URL on the file server. With <paramref name="reuse"/> the URL can be fetched
    /// more than once within its lifetime (one hour by default); otherwise it is single-use.
    /// </summary>
    public async Task<string> GetDownloadLinkAsync(string libraryId, string path, bool reuse = false, CancellationToken ct = default)
    {
        var link = await _t.GetAsync<string>(Url.Of($"api2/repos/{Url.Seg(libraryId)}/file/")
            .Q("p", SeafPath.Normalize(path)).Q("reuse", reuse ? "1" : null), ct).ConfigureAwait(false);
        return _t.RewriteFileServerUri(new Uri(link)).AbsoluteUri;
    }

    /// <summary>Opens a read stream for a file. Dispose the stream when done.</summary>
    public async Task<Stream> OpenReadAsync(string libraryId, string path, CancellationToken ct = default)
    {
        var link = await GetDownloadLinkAsync(libraryId, path, false, ct).ConfigureAwait(false);
        var response = await _t.SendRawAsync(HttpMethod.Get, link, null, HttpCompletionOption.ResponseHeadersRead, ct, authenticate: false).ConfigureAwait(false);
        return await SeafTransport.ReadStreamAsync(response, ct).ConfigureAwait(false);
    }

    /// <summary>Downloads a file into <paramref name="destination"/>.</summary>
    public async Task DownloadAsync(string libraryId, string path, Stream destination, CancellationToken ct = default)
    {
        await using var source = await OpenReadAsync(libraryId, path, ct).ConfigureAwait(false);
        await source.CopyToAsync(destination, ct).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadAllBytesAsync(string libraryId, string path, CancellationToken ct = default)
        => await _t.DownloadBytesAsync(await GetDownloadLinkAsync(libraryId, path, false, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>
    /// Downloads a text file. Decodes as UTF-8 by default: the file server often labels content with a wrong charset,
    /// so the response's declared encoding is deliberately ignored.
    /// </summary>
    public async Task<string> ReadAllTextAsync(string libraryId, string path, Encoding? encoding = null, CancellationToken ct = default)
        => await _t.DownloadTextAsync(await GetDownloadLinkAsync(libraryId, path, false, ct).ConfigureAwait(false), encoding, ct).ConfigureAwait(false);

    /// <summary>Returns a thumbnail image (PNG/JPEG bytes) for an image or video file.</summary>
    public async Task<byte[]> GetThumbnailAsync(string libraryId, string path, int size = 128, CancellationToken ct = default)
    {
        using var response = await _t.SendRawAsync(HttpMethod.Get, Url.Of($"api2/repos/{Url.Seg(libraryId)}/thumbnail/")
            .Q("p", SeafPath.Normalize(path)).Q("size", size), null, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    // ---------------- upload ----------------

    /// <summary>Returns a fresh upload URL for <paramref name="parentDir"/> (which must exist).</summary>
    public async Task<string> GetUploadLinkAsync(string libraryId, string parentDir = "/", CancellationToken ct = default)
    {
        var link = await _t.GetAsync<string>(Url.Of($"api2/repos/{Url.Seg(libraryId)}/upload-link/").Q("p", SeafPath.Normalize(parentDir)), ct).ConfigureAwait(false);
        return _t.RewriteFileServerUri(new Uri(link)).AbsoluteUri;
    }

    /// <summary>Returns a fresh URL for overwriting files in <paramref name="parentDir"/>.</summary>
    public async Task<string> GetUpdateLinkAsync(string libraryId, string parentDir = "/", CancellationToken ct = default)
    {
        var link = await _t.GetAsync<string>(Url.Of($"api2/repos/{Url.Seg(libraryId)}/update-link/").Q("p", SeafPath.Normalize(parentDir)), ct).ConfigureAwait(false);
        return _t.RewriteFileServerUri(new Uri(link)).AbsoluteUri;
    }

    /// <summary>
    /// Uploads a file into <paramref name="parentDir"/>. Unless <see cref="SeafUploadOptions.Replace"/> is set, an existing
    /// file is kept and the upload is stored under a new name; check <see cref="SeafUploadedFile.Name"/> for the final name.
    /// </summary>
    /// <remarks>Retries on transient failures are only possible when <paramref name="content"/> is seekable.</remarks>
    public async Task<SeafUploadedFile> UploadAsync(string libraryId, string parentDir, string fileName, Stream content, SeafUploadOptions? options = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        if (fileName.Contains('/') || fileName.Contains('\\')) throw new ArgumentException("File name must not contain '/' or '\\'.", nameof(fileName));
        ArgumentNullException.ThrowIfNull(content);
        options ??= new SeafUploadOptions();
        parentDir = SeafPath.Normalize(parentDir);

        // With create_parents, upload against the root and let relative_path create the missing folders.
        var linkDir = options.CreateParents ? SeafPath.Root : parentDir;
        var link = await GetUploadLinkAsync(libraryId, linkDir, ct).ConfigureAwait(false);

        var fields = new List<(string, string)> { ("parent_dir", linkDir) };
        if (options.CreateParents && parentDir != SeafPath.Root) fields.Add(("relative_path", parentDir.TrimStart('/')));
        if (options.Replace) fields.Add(("replace", "1"));

        var json = await _t.SendForStringAsync(HttpMethod.Post, Url.Of(link).Q("ret-json", "1"),
            Replayable(content, () => BuildMultipart(fields, fileName, content, options.ContentType, options.Progress)), ct).ConfigureAwait(false);

        var uploaded = SeafJson.Deserialize<List<SeafUploadedFile>>(json).FirstOrDefault()
            ?? throw new SeafException(HttpStatusCode.InternalServerError, "Upload returned no file information.", json);
        return uploaded with { LibraryId = libraryId, Path = SeafPath.Combine(parentDir, uploaded.Name) };
    }

    /// <summary>Uploads a local file.</summary>
    public async Task<SeafUploadedFile> UploadFileAsync(string libraryId, string parentDir, string localFilePath, SeafUploadOptions? options = null, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(localFilePath);
        return await UploadAsync(libraryId, parentDir, Path.GetFileName(localFilePath), fs, options, ct).ConfigureAwait(false);
    }

    /// <summary>Overwrites the content of an existing file, keeping its history. Returns the new object id.</summary>
    public async Task<string> UpdateAsync(string libraryId, string path, Stream content, IProgress<long>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        path = SeafPath.Normalize(path);
        var link = await GetUpdateLinkAsync(libraryId, SeafPath.GetParent(path), ct).ConfigureAwait(false);
        var body = await _t.SendForStringAsync(HttpMethod.Post, link,
            Replayable(content, () => BuildMultipart([("target_file", path)], SeafPath.GetName(path), content, null, progress)), ct).ConfigureAwait(false);
        return body.Trim().Trim('"');
    }

    /// <summary>Wraps a body factory so a retry rewinds the source stream, or fails clearly if it cannot be rewound.</summary>
    private static Func<HttpContent> Replayable(Stream content, Func<HttpContent> build)
    {
        var start = content.CanSeek ? content.Position : -1;
        var attempts = 0;
        return () =>
        {
            if (attempts++ > 0)
            {
                if (start < 0) throw new InvalidOperationException("The upload needs to be retried, but the source stream is not seekable.");
                content.Position = start;
            }
            return build();
        };
    }

    private static MultipartFormDataContent BuildMultipart(IEnumerable<(string Name, string Value)> fields, string fileName, Stream content, string? contentType, IProgress<long>? progress)
    {
        var form = SeafTransport.CreateMultipart();

        var file = new ProgressStreamContent(content, progress);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        file.Headers.TryAddWithoutValidation("Content-Disposition", $"form-data; name=\"file\"; filename=\"{EscapeQuoted(fileName)}\"");
        form.Add(file);

        foreach (var (name, value) in fields)
        {
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes(value));
            part.Headers.TryAddWithoutValidation("Content-Disposition", $"form-data; name=\"{name}\"");
            form.Add(part);
        }

        return form;
    }

    private static string EscapeQuoted(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ---------------- mutations ----------------

    /// <summary>Creates an empty file. Seafile auto-renames if the name is taken; the returned path is authoritative.</summary>
    public async Task<SeafFile> CreateAsync(string libraryId, string path, CancellationToken ct = default)
        => (await Operation(libraryId, path, ct, ("operation", "create")).ConfigureAwait(false)).ToFile();

    public async Task<SeafFile> RenameAsync(string libraryId, string path, string newName, CancellationToken ct = default)
        => (await Operation(libraryId, path, ct, ("operation", "rename"), ("newname", newName)).ConfigureAwait(false)).ToFile();

    /// <summary>Copies a file into <paramref name="destinationDir"/> (which must exist) and returns the copy.</summary>
    public async Task<SeafFile> CopyAsync(string libraryId, string path, string destinationLibraryId, string destinationDir, CancellationToken ct = default)
        => (await Operation(libraryId, path, ct, ("operation", "copy"), ("dst_repo", destinationLibraryId), ("dst_dir", SeafPath.Normalize(destinationDir))).ConfigureAwait(false)).ToFile();

    /// <summary>Moves a file into <paramref name="destinationDir"/> (which must exist) and returns it at its new location.</summary>
    public async Task<SeafFile> MoveAsync(string libraryId, string path, string destinationLibraryId, string destinationDir, CancellationToken ct = default)
        => (await Operation(libraryId, path, ct, ("operation", "move"), ("dst_repo", destinationLibraryId), ("dst_dir", SeafPath.Normalize(destinationDir))).ConfigureAwait(false)).ToFile();

    /// <summary>Deletes a file. Succeeds silently if the file does not exist.</summary>
    public Task DeleteAsync(string libraryId, string path, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, Url.Of(FileUrl(libraryId)).Q("p", SeafPath.Normalize(path)), null, ct);

    /// <summary>Locks a file so other users cannot modify it (Seafile Pro).</summary>
    public Task LockAsync(string libraryId, string path, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Put, Url.Of(FileUrl(libraryId)).Q("p", SeafPath.Normalize(path)), SeafTransport.Form(("operation", "lock")), ct);

    public Task UnlockAsync(string libraryId, string path, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Put, Url.Of(FileUrl(libraryId)).Q("p", SeafPath.Normalize(path)), SeafTransport.Form(("operation", "unlock")), ct);

    private Task<SeafOperationResult> Operation(string libraryId, string path, CancellationToken ct, params (string, string?)[] form)
        => _t.SendAsync<SeafOperationResult>(HttpMethod.Post, Url.Of(FileUrl(libraryId)).Q("p", SeafPath.Normalize(path)), SeafTransport.Form(form), ct);

    internal static Func<HttpContent> BuildReplayableUpload(IEnumerable<(string Name, string Value)> fields, string fileName, Stream content, string? contentType, IProgress<long>? progress)
        => Replayable(content, () => BuildMultipart(fields, fileName, content, contentType, progress));

    internal static Func<HttpContent> BuildUploadBody(IEnumerable<(string Name, string Value)> fields, string fileName, Stream content, SeafUploadOptions options)
    {
        var list = fields.ToList();
        return Replayable(content, () => BuildMultipart(list, fileName, content, options.ContentType, options.Progress));
    }

    private sealed record HistoryResponse { public List<SeafFileRevision> Data { get; init; } = []; }
}

/// <summary>Stream content that reports bytes written and does not dispose the caller's stream.</summary>
internal sealed class ProgressStreamContent(Stream source, IProgress<long>? progress, int bufferSize = 81920) : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken ct)
    {
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, bufferSize), ct).ConfigureAwait(false)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                total += read;
                progress?.Report(total);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override bool TryComputeLength(out long length)
    {
        if (source.CanSeek) { length = source.Length - source.Position; return true; }
        length = 0;
        return false;
    }

    // Intentionally do not dispose `source`: the caller owns it.
    protected override void Dispose(bool disposing) => base.Dispose(disposing);
}
