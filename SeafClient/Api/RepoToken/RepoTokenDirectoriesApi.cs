using System.Net;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api.RepoToken;

/// <summary>Directories of a repo-token library (<c>/api/v2.1/via-repo-token/dir/</c>).</summary>
public sealed class RepoTokenDirectoriesApi
{
    private readonly RepoTokenContext _c;
    internal RepoTokenDirectoriesApi(RepoTokenContext c) => _c = c;

    private const string DirUrl = RepoTokenContext.Base + "dir/";

    /// <summary>Lists a directory. Throws a 404 <see cref="SeafException"/> if it does not exist.</summary>
    /// <param name="recursive">Include all descendants. Each entry's <see cref="SeafDirEntry.Path"/> is its full path.</param>
    public async Task<IReadOnlyList<SeafDirEntry>> ListAsync(string path = "/", SeafListFilter filter = SeafListFilter.All, bool recursive = false, CancellationToken ct = default)
    {
        var parent = SeafPath.Normalize(path);
        var url = Url.Of(DirUrl)
            .Q("path", parent)
            .Q("type", filter switch { SeafListFilter.FilesOnly => "f", SeafListFilter.DirectoriesOnly => "d", _ => null })
            .Q("recursive", recursive ? "1" : null);

        var listing = _c.T.GetAsync<DirListResponse>(url, ct);
        var libraryId = _c.GetLibraryIdAsync(ct);
        var result = await listing.ConfigureAwait(false);
        var id = await libraryId.ConfigureAwait(false);
        for (var i = 0; i < result.DirentList.Count; i++)
        {
            var e = result.DirentList[i];
            result.DirentList[i] = e with { LibraryId = id, ParentDir = string.IsNullOrEmpty(e.ParentDir) ? parent : SeafPath.Normalize(e.ParentDir) };
        }
        return result.DirentList;
    }

    /// <summary>Returns true if the directory exists.</summary>
    public async Task<bool> ExistsAsync(string path, CancellationToken ct = default)
    {
        try
        {
            await _c.T.SendAsync(HttpMethod.Get, Url.Of(DirUrl).Q("path", SeafPath.Normalize(path)).Q("type", "d"), null, ct).ConfigureAwait(false);
            return true;
        }
        catch (SeafException e) when (e.IsNotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a directory. Throws a 409 <see cref="SeafException"/> if it already exists (the server itself would silently
    /// create <c>name (1)</c>), and a 404 if the parent is missing.
    /// </summary>
    public async Task<SeafDirectory> CreateAsync(string path, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        if (path == SeafPath.Root) throw new ArgumentException("Cannot create the library root.", nameof(path));
        if (await ExistsAsync(path, ct).ConfigureAwait(false))
            throw new SeafException(HttpStatusCode.Conflict, $"Folder {path} already exists.", method: HttpMethod.Post, requestUri: _c.T.Resolve(DirUrl));
        return await MkdirAsync(path, ct).ConfigureAwait(false);
    }

    /// <summary>Creates the directory and any missing parents; does nothing for existing ones.</summary>
    public async Task EnsureAsync(string path, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        if (path == SeafPath.Root || await ExistsAsync(path, ct).ConfigureAwait(false)) return;

        // No create_parents on this API: find the deepest existing ancestor, then create downwards.
        var segments = path.Trim('/').Split('/');
        var existing = 0;
        for (var i = segments.Length - 1; i > 0; i--)
        {
            if (await ExistsAsync("/" + string.Join('/', segments[..i]), ct).ConfigureAwait(false)) { existing = i; break; }
        }
        for (var i = existing + 1; i <= segments.Length; i++)
            await MkdirAsync("/" + string.Join('/', segments[..i]), ct).ConfigureAwait(false);
    }

    private async Task<SeafDirectory> MkdirAsync(string path, CancellationToken ct)
        => (await _c.T.SendAsync<SeafOperationResult>(HttpMethod.Post, Url.Of(DirUrl).Q("path", path),
            SeafTransport.Form(("operation", "mkdir")), ct).ConfigureAwait(false)).ToDirectory();

    /// <summary>Renames a directory. If the new name is taken, Seafile picks a free one (<c>name (1)</c>); the result is authoritative.</summary>
    public async Task<SeafDirectory> RenameAsync(string path, string newName, CancellationToken ct = default)
        => (await _c.T.SendAsync<SeafOperationResult>(HttpMethod.Post, Url.Of(DirUrl).Q("path", SeafPath.Normalize(path)),
            SeafTransport.Form(("operation", "rename"), ("newname", newName)), ct).ConfigureAwait(false)).ToDirectory();

    /// <summary>Restores a directory to its state in commit <paramref name="commitId"/> (see the library/file history).</summary>
    public Task RevertAsync(string path, string commitId, CancellationToken ct = default)
        => _c.T.SendAsync(HttpMethod.Post, Url.Of(DirUrl).Q("path", SeafPath.Normalize(path)),
            SeafTransport.Form(("operation", "revert"), ("commit_id", commitId)), ct);

    /// <summary>Deletes a directory and its contents. Throws a 404 <see cref="SeafException"/> if it does not exist.</summary>
    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        if (path == SeafPath.Root) throw new ArgumentException("Refusing to delete the library root.", nameof(path));
        return _c.T.SendAsync(HttpMethod.Delete, Url.Of(DirUrl).Q("path", path), null, ct);
    }

    /// <summary>Deletes a directory if it exists. Returns true if something was deleted.</summary>
    public async Task<bool> DeleteIfExistsAsync(string path, CancellationToken ct = default)
    {
        try { await DeleteAsync(path, ct).ConfigureAwait(false); return true; }
        catch (SeafException e) when (e.IsNotFound) { return false; }
    }

    /// <summary>
    /// Moves a directory into <paramref name="destinationDir"/>, merging its contents into an existing folder of the same
    /// name there instead of creating <c>name (1)</c>.
    /// </summary>
    public Task MoveMergeAsync(string path, string destinationDir, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        return _c.T.SendAsync(HttpMethod.Post, RepoTokenContext.Base + "move-dir/", SeafTransport.Json(new
        {
            src_parent_dir = SeafPath.GetParent(path),
            src_dirent_name = SeafPath.GetName(path),
            dst_parent_dir = SeafPath.Normalize(destinationDir),
        }), ct);
    }

    private sealed record DirListResponse { public List<SeafDirEntry> DirentList { get; init; } = []; }
}
