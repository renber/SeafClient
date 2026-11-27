using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>Library ("repo") endpoints.</summary>
public sealed partial class LibrariesApi
{
    private readonly SeafTransport _t;
    internal LibrariesApi(SeafTransport t) => _t = t;

    /// <summary>
    /// Lists libraries visible to the current user. With no <paramref name="type"/>, all kinds are returned and a library can
    /// appear more than once (e.g. once as <c>mine</c> and again as <c>group</c> if the owner also shared it with a group).
    /// </summary>
    public async Task<IReadOnlyList<SeafLibrary>> ListAsync(SeafLibraryType? type = null, CancellationToken ct = default)
    {
        var url = Url.Of("api/v2.1/repos/").Q("type", type?.ToString().ToLowerInvariant());
        var result = await _t.GetAsync<ReposResponse>(url, ct).ConfigureAwait(false);
        return result.Repos;
    }

    /// <summary>Lists all visible libraries, with duplicates (same library reachable several ways) removed. Owned entries win.</summary>
    public async Task<IReadOnlyList<SeafLibrary>> ListDistinctAsync(CancellationToken ct = default)
    {
        var all = await ListAsync(null, ct).ConfigureAwait(false);
        return all.OrderBy(l => l.Type == "mine" ? 0 : 1).DistinctBy(l => l.Id).ToList();
    }

    /// <summary>Finds a visible library by exact name; returns null if none. Throws if the name is ambiguous.</summary>
    public async Task<SeafLibrary?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        var matches = (await ListDistinctAsync(ct).ConfigureAwait(false)).Where(l => l.Name == name).ToList();
        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException($"{matches.Count} libraries are named '{name}'."),
        };
    }

    public Task<SeafLibrary> GetAsync(string libraryId, CancellationToken ct = default)
        => _t.GetAsync<SeafLibrary>($"api/v2.1/repos/{Url.Seg(libraryId)}/", ct);

    /// <summary>Creates a library and returns its details. Pass <paramref name="password"/> to create an encrypted library.</summary>
    public async Task<SeafLibrary> CreateAsync(string name, string? description = null, string? password = null, CancellationToken ct = default)
    {
        var created = await _t.SendAsync<SeafLibrary>(HttpMethod.Post, "api2/repos/",
            SeafTransport.Form(("name", name), ("desc", description), ("passwd", password)), ct).ConfigureAwait(false);
        return await GetAsync(created.Id, ct).ConfigureAwait(false);
    }

    public Task RenameAsync(string libraryId, string newName, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Post, $"api2/repos/{Url.Seg(libraryId)}/?op=rename", SeafTransport.Form(("repo_name", newName)), ct);

    /// <summary>Deletes a library (it goes to the server's library trash).</summary>
    public Task DeleteAsync(string libraryId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, $"api/v2.1/repos/{Url.Seg(libraryId)}/", null, ct);

    /// <summary>Unlocks an encrypted library for this session's user for a limited time.</summary>
    public Task DecryptAsync(string libraryId, string password, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Post, $"api/v2.1/repos/{Url.Seg(libraryId)}/set-password/", SeafTransport.Form(("password", password)), ct);

    /// <summary>Returns the id of the user's default library ("My Library"), or null if there is none.</summary>
    public async Task<string?> GetDefaultIdAsync(CancellationToken ct = default)
    {
        var r = await _t.GetAsync<DefaultRepoResponse>("api2/default-repo/", ct).ConfigureAwait(false);
        return r.Exists ? r.RepoId : null;
    }

    /// <summary>Returns the default library id, creating the default library if needed.</summary>
    public async Task<string> EnsureDefaultIdAsync(CancellationToken ct = default)
    {
        var r = await _t.SendAsync<DefaultRepoResponse>(HttpMethod.Post, "api2/default-repo/", null, ct).ConfigureAwait(false);
        return r.RepoId ?? throw new SeafException(System.Net.HttpStatusCode.InternalServerError, "Server did not return a default library id.");
    }

    // ---- sharing a library (or a folder in it) with users / groups ----

    /// <summary>Lists users and groups a library folder (default: the whole library) is shared with.</summary>
    public async Task<IReadOnlyList<SeafSharedItem>> ListSharesAsync(string libraryId, string path = "/", CancellationToken ct = default)
    {
        var users = _t.GetAsync<List<SeafSharedItem>>(Url.Of($"api2/repos/{Url.Seg(libraryId)}/dir/shared_items/").Q("p", SeafPath.Normalize(path)).Q("share_type", "user"), ct);
        var groups = _t.GetAsync<List<SeafSharedItem>>(Url.Of($"api2/repos/{Url.Seg(libraryId)}/dir/shared_items/").Q("p", SeafPath.Normalize(path)).Q("share_type", "group"), ct);
        await Task.WhenAll(users, groups).ConfigureAwait(false);
        return [.. users.Result, .. groups.Result];
    }

    public Task ShareWithGroupAsync(string libraryId, int groupId, SeafSharePermission permission = SeafSharePermission.Rw, string path = "/", CancellationToken ct = default)
        => Share(libraryId, path, ("share_type", "group"), ("group_id", groupId.ToString(System.Globalization.CultureInfo.InvariantCulture)), permission, ct);

    public Task ShareWithUserAsync(string libraryId, string userEmail, SeafSharePermission permission = SeafSharePermission.Rw, string path = "/", CancellationToken ct = default)
        => Share(libraryId, path, ("share_type", "user"), ("username", userEmail), permission, ct);

    public Task UnshareFromGroupAsync(string libraryId, int groupId, string path = "/", CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, Url.Of($"api2/repos/{Url.Seg(libraryId)}/dir/shared_items/")
            .Q("p", SeafPath.Normalize(path)).Q("share_type", "group").Q("group_id", groupId), null, ct);

    public Task UnshareFromUserAsync(string libraryId, string userEmail, string path = "/", CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, Url.Of($"api2/repos/{Url.Seg(libraryId)}/dir/shared_items/")
            .Q("p", SeafPath.Normalize(path)).Q("share_type", "user").Q("username", userEmail), null, ct);

    private async Task Share(string libraryId, string path, (string, string?) type, (string, string?) target, SeafSharePermission permission, CancellationToken ct)
    {
        var result = await _t.SendAsync<ShareResult>(HttpMethod.Put, Url.Of($"api2/repos/{Url.Seg(libraryId)}/dir/shared_items/").Q("p", SeafPath.Normalize(path)),
            SeafTransport.Form(type, target, ("permission", permission.ToString().ToLowerInvariant())), ct).ConfigureAwait(false);
        if (result.Failed is { Count: > 0 } failed)
            throw new SeafException(System.Net.HttpStatusCode.BadRequest, string.Join("; ", failed.Select(f => f.ErrorMsg)));
    }

    private sealed record ReposResponse { public List<SeafLibrary> Repos { get; init; } = []; }
    private sealed record DefaultRepoResponse { public bool Exists { get; init; } public string? RepoId { get; init; } }
    private sealed record ShareResult { public List<ShareFailure>? Failed { get; init; } }
    private sealed record ShareFailure { public string ErrorMsg { get; init; } = ""; }
}
