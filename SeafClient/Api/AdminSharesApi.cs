using System.Globalization;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>Share any library (or folder in it) with users and groups, regardless of who owns it.</summary>
public sealed class AdminSharesApi
{
    private const string Base = "api/v2.1/admin/shares/";

    private readonly SeafTransport _t;
    internal AdminSharesApi(SeafTransport t) => _t = t;

    /// <summary>Lists user and group shares of a library folder (default: the whole library).</summary>
    public async Task<IReadOnlyList<SeafAdminShare>> ListAsync(string libraryId, string path = "/", CancellationToken ct = default)
    {
        var users = _t.GetAsync<List<SeafAdminShare>>(Url.Of(Base).Q("repo_id", libraryId).Q("path", SeafPath.Normalize(path)).Q("share_type", "user"), ct);
        var groups = _t.GetAsync<List<SeafAdminShare>>(Url.Of(Base).Q("repo_id", libraryId).Q("path", SeafPath.Normalize(path)).Q("share_type", "group"), ct);
        await Task.WhenAll(users, groups).ConfigureAwait(false);
        return [.. users.Result, .. groups.Result];
    }

    /// <summary>Shares a library folder with users; per-user failures are reported in the result rather than thrown.</summary>
    public Task<SeafAdminShareResult> ShareWithUsersAsync(string libraryId, IEnumerable<string> userEmails, SeafSharePermission permission = SeafSharePermission.Rw, string path = "/", CancellationToken ct = default)
        => Share(libraryId, path, "user", userEmails, permission, ct);

    /// <summary>Shares a library folder with groups; per-group failures are reported in the result rather than thrown.</summary>
    public Task<SeafAdminShareResult> ShareWithGroupsAsync(string libraryId, IEnumerable<int> groupIds, SeafSharePermission permission = SeafSharePermission.Rw, string path = "/", CancellationToken ct = default)
        => Share(libraryId, path, "group", groupIds.Select(g => g.ToString(CultureInfo.InvariantCulture)), permission, ct);

    /// <summary>Changes the permission of an existing user share.</summary>
    public Task<SeafAdminShare> UpdateUserPermissionAsync(string libraryId, string userEmail, SeafSharePermission permission, string path = "/", CancellationToken ct = default)
        => Update(libraryId, path, "user", userEmail, permission, ct);

    /// <summary>Changes the permission of an existing group share.</summary>
    public Task<SeafAdminShare> UpdateGroupPermissionAsync(string libraryId, int groupId, SeafSharePermission permission, string path = "/", CancellationToken ct = default)
        => Update(libraryId, path, "group", groupId.ToString(CultureInfo.InvariantCulture), permission, ct);

    public Task UnshareFromUserAsync(string libraryId, string userEmail, string path = "/", CancellationToken ct = default)
        => Unshare(libraryId, path, "user", userEmail, ct);

    public Task UnshareFromGroupAsync(string libraryId, int groupId, string path = "/", CancellationToken ct = default)
        => Unshare(libraryId, path, "group", groupId.ToString(CultureInfo.InvariantCulture), ct);

    private Task<SeafAdminShareResult> Share(string libraryId, string path, string type, IEnumerable<string> targets, SeafSharePermission permission, CancellationToken ct)
        => _t.SendAsync<SeafAdminShareResult>(HttpMethod.Post, Base, SeafTransport.Form([
            ("repo_id", libraryId), ("path", SeafPath.Normalize(path)), ("share_type", type), ("permission", Perm(permission)),
            .. targets.Select(t => ("share_to", (string?)t))]), ct);

    private Task<SeafAdminShare> Update(string libraryId, string path, string type, string target, SeafSharePermission permission, CancellationToken ct)
        => _t.SendAsync<SeafAdminShare>(HttpMethod.Put, Base, SeafTransport.Form(
            ("repo_id", libraryId), ("path", SeafPath.Normalize(path)), ("share_type", type), ("share_to", target), ("permission", Perm(permission))), ct);

    private Task Unshare(string libraryId, string path, string type, string target, CancellationToken ct)
        => _t.SendAsync(HttpMethod.Delete, Base, SeafTransport.Form(
            ("repo_id", libraryId), ("path", SeafPath.Normalize(path)), ("share_type", type), ("share_to", target)), ct);

    private static string Perm(SeafSharePermission p) => p.ToString().ToLowerInvariant();
}
