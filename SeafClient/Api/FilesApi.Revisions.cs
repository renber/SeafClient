using System.Text;
using SeafClient.Internal;

namespace SeafClient.Api;

// Old versions of a file: revert and download.
public sealed partial class FilesApi
{
    /// <summary>
    /// Restores a file to the version stored in <paramref name="commitId"/> (see <see cref="GetHistoryAsync"/>).
    /// Creates a new version; if the file was deleted it is recreated.
    /// </summary>
    public Task RevertAsync(string libraryId, string path, string commitId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Post, Url.Of(FileUrl(libraryId)).Q("p", SeafPath.Normalize(path)),
            SeafTransport.Form(("operation", "revert"), ("commit_id", commitId)), ct);

    /// <summary>Returns a temporary, single-use download URL for the file as it was at <paramref name="commitId"/>.</summary>
    public async Task<string> GetRevisionDownloadLinkAsync(string libraryId, string path, string commitId, CancellationToken ct = default)
    {
        var link = await _t.GetAsync<string>(Url.Of($"api2/repos/{Url.Seg(libraryId)}/file/revision/")
            .Q("p", SeafPath.Normalize(path)).Q("commit_id", commitId), ct).ConfigureAwait(false);
        return _t.RewriteFileServerUri(new Uri(link)).AbsoluteUri;
    }

    /// <summary>Opens a read stream for an old version of a file. Dispose the stream when done.</summary>
    public async Task<Stream> OpenRevisionReadAsync(string libraryId, string path, string commitId, CancellationToken ct = default)
    {
        var link = await GetRevisionDownloadLinkAsync(libraryId, path, commitId, ct).ConfigureAwait(false);
        var response = await _t.SendRawAsync(HttpMethod.Get, link, null, HttpCompletionOption.ResponseHeadersRead, ct, authenticate: false).ConfigureAwait(false);
        return await SeafTransport.ReadStreamAsync(response, ct).ConfigureAwait(false);
    }

    /// <summary>Downloads an old version of a file into memory.</summary>
    public async Task<byte[]> ReadRevisionBytesAsync(string libraryId, string path, string commitId, CancellationToken ct = default)
        => await _t.DownloadBytesAsync(await GetRevisionDownloadLinkAsync(libraryId, path, commitId, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>Downloads an old version of a text file, decoded as UTF-8 by default (see <see cref="ReadAllTextAsync"/>).</summary>
    public async Task<string> ReadRevisionTextAsync(string libraryId, string path, string commitId, Encoding? encoding = null, CancellationToken ct = default)
        => await _t.DownloadTextAsync(await GetRevisionDownloadLinkAsync(libraryId, path, commitId, ct).ConfigureAwait(false), encoding, ct).ConfigureAwait(false);
}
