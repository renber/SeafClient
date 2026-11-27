using System.Net;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>Directory endpoints.</summary>
/// <remarks>
/// Seafile never fails a mkdir because the target exists: it silently creates <c>name (1)</c> instead.
/// <see cref="CreateAsync"/> therefore checks first and throws a 409 <see cref="SeafException"/>, and
/// <see cref="EnsureAsync"/> is the idempotent "create if missing" variant.
/// </remarks>
public sealed class DirectoriesApi
{
    private readonly SeafTransport _t;
    private readonly ItemsApi _items;

    internal DirectoriesApi(SeafTransport t, ItemsApi items)
    {
        _t = t;
        _items = items;
    }

    private static string DirUrl(string libraryId, string version = "api/v2.1") => $"{version}/repos/{Url.Seg(libraryId)}/dir/";

    /// <summary>Lists a directory. Throws a 404 <see cref="SeafException"/> if it does not exist.</summary>
    /// <param name="recursive">Include all descendants. Each entry's <see cref="SeafDirEntry.Path"/> is its full path.</param>
    public async Task<IReadOnlyList<SeafDirEntry>> ListAsync(string libraryId, string path = "/", SeafListFilter filter = SeafListFilter.All, bool recursive = false, CancellationToken ct = default)
    {
        var url = Url.Of(DirUrl(libraryId))
            .Q("p", SeafPath.Normalize(path))
            .Q("t", filter switch { SeafListFilter.FilesOnly => "f", SeafListFilter.DirectoriesOnly => "d", _ => null })
            .Q("recursive", recursive ? "1" : null);

        var result = await _t.GetAsync<DirListResponse>(url, ct).ConfigureAwait(false);
        var parent = SeafPath.Normalize(path);
        for (var i = 0; i < result.DirentList.Count; i++)
        {
            var e = result.DirentList[i];
            result.DirentList[i] = e with { LibraryId = libraryId, ParentDir = string.IsNullOrEmpty(e.ParentDir) ? parent : SeafPath.Normalize(e.ParentDir) };
        }
        return result.DirentList;
    }

    /// <summary>Lists the contents of a directory entry returned by an earlier listing.</summary>
    public Task<IReadOnlyList<SeafDirEntry>> ListAsync(SeafDirEntry directory, SeafListFilter filter = SeafListFilter.All, bool recursive = false, CancellationToken ct = default)
    {
        if (!directory.IsDirectory) throw new ArgumentException("The entry is not a directory.", nameof(directory));
        return ListAsync(directory.LibraryId, directory.Path, filter, recursive, ct);
    }

    /// <summary>Gets directory details. Throws a 404 <see cref="SeafException"/> if it does not exist.</summary>
    public Task<SeafDirectory> GetAsync(string libraryId, string path, CancellationToken ct = default)
        => _t.GetAsync<SeafDirectory>(Url.Of($"api/v2.1/repos/{Url.Seg(libraryId)}/dir/detail/").Q("path", SeafPath.Normalize(path)), ct);

    /// <summary>Gets directory details, or null if the directory does not exist.</summary>
    public async Task<SeafDirectory?> TryGetAsync(string libraryId, string path, CancellationToken ct = default)
    {
        try { return await GetAsync(libraryId, path, ct).ConfigureAwait(false); }
        catch (SeafException e) when (e.IsNotFound) { return null; }
    }

    public async Task<bool> ExistsAsync(string libraryId, string path, CancellationToken ct = default)
        => await TryGetAsync(libraryId, path, ct).ConfigureAwait(false) != null;

    /// <summary>
    /// Creates a directory. Throws a 409 <see cref="SeafException"/> if it already exists, and a 404 if the parent is
    /// missing and <paramref name="createParents"/> is false.
    /// </summary>
    public async Task<SeafDirectory> CreateAsync(string libraryId, string path, bool createParents = false, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        if (path == SeafPath.Root) throw new ArgumentException("Cannot create the library root.", nameof(path));

        if (await ExistsAsync(libraryId, path, ct).ConfigureAwait(false))
            throw new SeafException(HttpStatusCode.Conflict, $"Folder {path} already exists.", method: HttpMethod.Post, requestUri: _t.Resolve(DirUrl(libraryId)));

        return await MkdirAsync(libraryId, path, createParents, ct).ConfigureAwait(false);
    }

    /// <summary>Returns the directory, creating it (and any missing parents) if it does not exist.</summary>
    public async Task<SeafDirectory> EnsureAsync(string libraryId, string path, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        return await TryGetAsync(libraryId, path, ct).ConfigureAwait(false)
            ?? await MkdirAsync(libraryId, path, createParents: true, ct).ConfigureAwait(false);
    }

    private async Task<SeafDirectory> MkdirAsync(string libraryId, string path, bool createParents, CancellationToken ct)
    {
        if (createParents)
        {
            // Only api2 honours create_parents. It returns no body, so read the result back.
            await _t.SendAsync(HttpMethod.Post, Url.Of(DirUrl(libraryId, "api2")).Q("p", path),
                SeafTransport.Form(("operation", "mkdir"), ("create_parents", "true")), ct).ConfigureAwait(false);
            return await GetAsync(libraryId, path, ct).ConfigureAwait(false);
        }

        var result = await _t.SendAsync<SeafOperationResult>(HttpMethod.Post, Url.Of(DirUrl(libraryId)).Q("p", path),
            SeafTransport.Form(("operation", "mkdir")), ct).ConfigureAwait(false);
        return result.ToDirectory();
    }

    /// <summary>Renames a directory in place and returns its new details.</summary>
    public async Task<SeafDirectory> RenameAsync(string libraryId, string path, string newName, CancellationToken ct = default)
    {
        var result = await _t.SendAsync<SeafOperationResult>(HttpMethod.Post, Url.Of(DirUrl(libraryId)).Q("p", SeafPath.Normalize(path)),
            SeafTransport.Form(("operation", "rename"), ("newname", newName)), ct).ConfigureAwait(false);
        return result.ToDirectory();
    }

    /// <summary>Deletes a directory and its contents. Throws a 404 <see cref="SeafException"/> if it does not exist.</summary>
    public Task DeleteAsync(string libraryId, string path, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        if (path == SeafPath.Root) throw new ArgumentException("Refusing to delete the library root.", nameof(path));
        return _t.SendAsync(HttpMethod.Delete, Url.Of(DirUrl(libraryId)).Q("p", path), null, ct);
    }

    /// <summary>Deletes a directory if it exists. Returns true if something was deleted.</summary>
    public async Task<bool> DeleteIfExistsAsync(string libraryId, string path, CancellationToken ct = default)
    {
        try
        {
            await DeleteAsync(libraryId, path, ct).ConfigureAwait(false);
            return true;
        }
        catch (SeafException e) when (e.IsNotFound)
        {
            return false;
        }
    }

    /// <summary>Copies a directory (recursively) into <paramref name="destinationDir"/>.</summary>
    public Task CopyAsync(string libraryId, string path, string destinationLibraryId, string destinationDir, CancellationToken ct = default)
        => _items.CopyAsync(libraryId, SeafPath.GetParent(path), [SeafPath.GetName(path)], destinationLibraryId, destinationDir, ct);

    /// <summary>Moves a directory into <paramref name="destinationDir"/>.</summary>
    public Task MoveAsync(string libraryId, string path, string destinationLibraryId, string destinationDir, CancellationToken ct = default)
        => _items.MoveAsync(libraryId, SeafPath.GetParent(path), [SeafPath.GetName(path)], destinationLibraryId, destinationDir, ct);

    /// <summary>
    /// Moves the directory <paramref name="sourcePath"/> into <paramref name="destinationParentDir"/>, merging it with a
    /// same-named directory there instead of creating <c>name (1)</c>. Without a same-named directory it behaves like a move.
    /// </summary>
    public Task MoveMergeAsync(string sourceLibraryId, string sourcePath, string destinationLibraryId, string destinationParentDir, CancellationToken ct = default)
    {
        sourcePath = SeafPath.Normalize(sourcePath);
        if (sourcePath == "/") throw new ArgumentException("The library root cannot be moved.", nameof(sourcePath));
        return _t.SendAsync(HttpMethod.Post, "api/v2.1/move-folder-merge/", SeafTransport.Json(new
        {
            src_repo_id = sourceLibraryId,
            src_parent_dir = SeafPath.GetParent(sourcePath),
            src_dirent_name = SeafPath.GetName(sourcePath),
            dst_repo_id = destinationLibraryId,
            dst_parent_dir = SeafPath.Normalize(destinationParentDir),
        }), ct);
    }

    /// <summary>
    /// Restores a directory (recursively) to its state at <paramref name="commitId"/>. If the directory no longer exists
    /// it is recreated. Creates a new commit; nothing is lost.
    /// </summary>
    public Task RevertAsync(string libraryId, string path, string commitId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Put, $"api2/repos/{Url.Seg(libraryId)}/dir/revert/",
            SeafTransport.Form(("p", SeafPath.Normalize(path)), ("commit_id", commitId)), ct);

    private sealed record DirListResponse { public List<SeafDirEntry> DirentList { get; init; } = []; }
}
