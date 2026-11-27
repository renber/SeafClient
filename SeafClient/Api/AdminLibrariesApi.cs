using System.Globalization;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>All libraries on the server, and the system library trash, via the admin API.</summary>
public sealed class AdminLibrariesApi
{
    private readonly SeafTransport _t;
    internal AdminLibrariesApi(SeafTransport t) => _t = t;

    private static string Base(string libraryId) => $"api/v2.1/admin/libraries/{Url.Seg(libraryId)}/";

    /// <summary>Lists all libraries one page at a time.</summary>
    /// <param name="orderBy"><c>size</c> or <c>file_count</c> (descending); null for the server default.</param>
    public async Task<SeafPage<SeafAdminLibrary>> ListAsync(int page = 1, int perPage = 100, string? orderBy = null, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<ReposResponse>(Url.Of("api/v2.1/admin/libraries/").Q("page", page).Q("per_page", perPage).Q("order_by", orderBy), ct).ConfigureAwait(false);
        return new SeafPage<SeafAdminLibrary> { Items = r.Repos, Page = page, PerPage = perPage, HasNextPage = r.PageInfo?.HasNextPage ?? false };
    }

    /// <summary>Finds libraries by (partial) name and/or exact owner. Not paginated.</summary>
    public async Task<IReadOnlyList<SeafAdminLibrary>> FindAsync(string? name = null, string? ownerEmail = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(ownerEmail))
            throw new ArgumentException("Pass a name and/or an owner.");
        return (await _t.GetAsync<ReposResponse>(Url.Of("api/v2.1/admin/libraries/").Q("name", name).Q("owner", ownerEmail), ct).ConfigureAwait(false)).Repos;
    }

    /// <summary>Searches libraries by name (<c>admin/search-library/</c>).</summary>
    public async Task<IReadOnlyList<SeafAdminLibrary>> SearchAsync(string query, CancellationToken ct = default)
        => (await _t.GetAsync<SearchResponse>(Url.Of("api/v2.1/admin/search-library/").Q("query", query), ct).ConfigureAwait(false)).RepoList;

    public Task<SeafAdminLibrary> GetAsync(string libraryId, CancellationToken ct = default)
        => _t.GetAsync<SeafAdminLibrary>(Base(libraryId), ct);

    /// <summary>Creates a library owned by <paramref name="ownerEmail"/> (default: the current user).</summary>
    public Task<SeafAdminLibrary> CreateAsync(string name, string? ownerEmail = null, CancellationToken ct = default)
        => _t.SendAsync<SeafAdminLibrary>(HttpMethod.Post, "api/v2.1/admin/libraries/", SeafTransport.Form(("name", name), ("owner", ownerEmail)), ct);

    /// <summary>Renames a library.</summary>
    public Task<SeafAdminLibrary> RenameAsync(string libraryId, string newName, CancellationToken ct = default)
        => _t.SendAsync<SeafAdminLibrary>(HttpMethod.Put, Base(libraryId), SeafTransport.Form(("name", newName)), ct);

    /// <summary>Sets a library to normal or read-only.</summary>
    public Task<SeafAdminLibrary> SetStatusAsync(string libraryId, SeafLibraryStatus status, CancellationToken ct = default)
        => _t.SendAsync<SeafAdminLibrary>(HttpMethod.Put, Base(libraryId),
            SeafTransport.Form(("status", status == SeafLibraryStatus.ReadOnly ? "read-only" : "normal")), ct);

    /// <summary>Transfers a library to another user (or a department, as <c>{id}@seafile_group</c>).</summary>
    /// <param name="keepShares">Keep the library's existing shares after the transfer.</param>
    public Task<SeafAdminLibrary> TransferAsync(string libraryId, string newOwner, bool keepShares = false, CancellationToken ct = default)
        => _t.SendAsync<SeafAdminLibrary>(HttpMethod.Put, Base(libraryId),
            SeafTransport.Form(("owner", newOwner), ("reshare", keepShares ? "true" : "false")), ct);

    /// <summary>Deletes a library (it goes to the system library trash).</summary>
    public Task DeleteAsync(string libraryId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, Base(libraryId), null, ct);

    /// <summary>Gets how many days of history the library keeps (-1 = forever, 0 = none).</summary>
    public async Task<int> GetHistoryLimitAsync(string libraryId, CancellationToken ct = default)
        => (await _t.GetAsync<HistoryLimit>(Base(libraryId) + "history-limit/", ct).ConfigureAwait(false)).KeepDays;

    /// <summary>Sets how many days of history the library keeps (-1 = forever, 0 = none). Returns the new value.</summary>
    public async Task<int> SetHistoryLimitAsync(string libraryId, int keepDays, CancellationToken ct = default)
        => (await _t.SendAsync<HistoryLimit>(HttpMethod.Put, Base(libraryId) + "history-limit/",
            SeafTransport.Form(("keep_days", keepDays.ToString(CultureInfo.InvariantCulture))), ct).ConfigureAwait(false)).KeepDays;

    /// <summary>
    /// Creates a folder in any library. Requires <c>ENABLE_SYS_ADMIN_VIEW_REPO = True</c> on the server (otherwise 403
    /// "Feature disabled.") and is refused for encrypted libraries.
    /// </summary>
    public Task<SeafAdminDirent> CreateFolderAsync(string libraryId, string parentDir, string name, CancellationToken ct = default)
        => _t.SendAsync<SeafAdminDirent>(HttpMethod.Post, Url.Of(Base(libraryId) + "dirents/").Q("parent_dir", SeafPath.Normalize(parentDir)),
            SeafTransport.Form(("obj_name", name)), ct);

    // ---- system library trash ----

    /// <summary>Lists deleted libraries one page at a time, optionally only those of one owner (then not paginated).</summary>
    public async Task<SeafPage<SeafTrashLibrary>> ListTrashAsync(int page = 1, int perPage = 100, string? ownerEmail = null, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<TrashResponse>(Url.Of("api/v2.1/admin/trash-libraries/").Q("page", page).Q("per_page", perPage).Q("owner", ownerEmail), ct).ConfigureAwait(false);
        return new SeafPage<SeafTrashLibrary> { Items = r.Repos, Page = page, PerPage = perPage, HasNextPage = r.PageInfo?.HasNextPage ?? false };
    }

    /// <summary>Restores a deleted library from the trash.</summary>
    public Task RestoreFromTrashAsync(string libraryId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Put, $"api/v2.1/admin/trash-libraries/{Url.Seg(libraryId)}/", null, ct);

    /// <summary>Permanently deletes one library from the trash. Cannot be undone.</summary>
    public Task PurgeFromTrashAsync(string libraryId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, $"api/v2.1/admin/trash-libraries/{Url.Seg(libraryId)}/", null, ct);

    /// <summary>
    /// Permanently deletes every library in the trash, or only those of <paramref name="ownerEmail"/>. Cannot be undone.
    /// </summary>
    public Task EmptyTrashAsync(string? ownerEmail = null, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, "api/v2.1/admin/trash-libraries/", ownerEmail == null ? null : SeafTransport.Form(("owner", ownerEmail)), ct);

    private sealed record ReposResponse { public List<SeafAdminLibrary> Repos { get; init; } = []; public SeafPageInfo? PageInfo { get; init; } }
    private sealed record SearchResponse { public List<SeafAdminLibrary> RepoList { get; init; } = []; }
    private sealed record TrashResponse { public List<SeafTrashLibrary> Repos { get; init; } = []; public SeafPageInfo? PageInfo { get; init; } }
    private sealed record HistoryLimit { public int KeepDays { get; init; } }
}
