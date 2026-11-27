using System.Globalization;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>All groups on the server, via the admin API.</summary>
public sealed class AdminGroupsApi
{
    private readonly SeafTransport _t;
    internal AdminGroupsApi(SeafTransport t) => _t = t;

    private static string Base(int groupId) => $"api/v2.1/admin/groups/{groupId}/";

    /// <summary>Lists groups (and departments) one page at a time. Pass <paramref name="name"/> to filter by name.</summary>
    public async Task<SeafListPage<SeafAdminGroup>> ListAsync(int page = 1, int perPage = 100, string? name = null, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<GroupListResponse>(Url.Of("api/v2.1/admin/groups/").Q("page", page).Q("per_page", perPage).Q("name", name), ct).ConfigureAwait(false);
        return new SeafListPage<SeafAdminGroup> { Items = r.Groups, Page = page, PerPage = perPage, HasNextPage = r.PageInfo?.HasNextPage ?? false };
    }

    /// <summary>Searches groups by name.</summary>
    public async Task<IReadOnlyList<SeafAdminGroup>> SearchAsync(string query, CancellationToken ct = default)
        => (await _t.GetAsync<SearchResponse>(Url.Of("api/v2.1/admin/search-group/").Q("query", query), ct).ConfigureAwait(false)).GroupList;

    /// <summary>Creates a group. <paramref name="ownerEmail"/> defaults to the current user.</summary>
    public Task<SeafAdminGroup> CreateAsync(string name, string? ownerEmail = null, CancellationToken ct = default)
        => _t.SendAsync<SeafAdminGroup>(HttpMethod.Post, "api/v2.1/admin/groups/", SeafTransport.Form(("group_name", name), ("group_owner", ownerEmail)), ct);

    /// <summary>Renames a group.</summary>
    public Task<SeafAdminGroup> RenameAsync(int groupId, string newName, CancellationToken ct = default)
        => _t.SendAsync<SeafAdminGroup>(HttpMethod.Put, Base(groupId), SeafTransport.Form(("name", newName)), ct);

    /// <summary>Transfers a group to a new owner.</summary>
    public Task<SeafAdminGroup> TransferAsync(int groupId, string newOwnerEmail, CancellationToken ct = default)
        => _t.SendAsync<SeafAdminGroup>(HttpMethod.Put, Base(groupId), SeafTransport.Form(("new_owner", newOwnerEmail)), ct);

    /// <summary>
    /// Sets a department's quota in bytes (-2 removes it). Only valid for departments. The server stores the number as given
    /// and reports it back unchanged in <see cref="SeafAdminGroup.Quota"/>.
    /// </summary>
    public Task<SeafAdminGroup> SetQuotaAsync(int groupId, long quotaBytes, CancellationToken ct = default)
        => _t.SendAsync<SeafAdminGroup>(HttpMethod.Put, Base(groupId), SeafTransport.Form(("quota", quotaBytes.ToString(CultureInfo.InvariantCulture))), ct);

    /// <summary>Moves a department under another department.</summary>
    public Task MoveDepartmentAsync(int groupId, int targetGroupId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Put, Base(groupId), SeafTransport.Form(("target_group_id", targetGroupId.ToString(CultureInfo.InvariantCulture))), ct);

    public Task DeleteAsync(int groupId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, Base(groupId), null, ct);

    /// <summary>Lists a page of group members.</summary>
    public async Task<SeafPage<SeafGroupMember>> ListMembersAsync(int groupId, int page = 1, int perPage = 100, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<MembersResponse>(Url.Of(Base(groupId) + "members/").Q("page", page).Q("per_page", perPage), ct).ConfigureAwait(false);
        return new SeafPage<SeafGroupMember> { Items = r.Members, TotalCount = r.TotalCount, Page = page, PerPage = perPage };
    }

    /// <summary>Adds users to a group; per-user failures are reported in the result rather than thrown.</summary>
    public Task<SeafBulkMemberResult> AddMembersAsync(int groupId, IEnumerable<string> userEmails, CancellationToken ct = default)
        => _t.SendAsync<SeafBulkMemberResult>(HttpMethod.Post, Base(groupId) + "members/",
            SeafTransport.Form([.. userEmails.Select(e => ("email", (string?)e))]), ct);

    public Task RemoveMemberAsync(int groupId, string userEmail, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, $"{Base(groupId)}members/{Url.Seg(userEmail)}/", null, ct);

    /// <summary>Promotes or demotes a member to/from group admin.</summary>
    public Task<SeafGroupMember> SetAdminAsync(int groupId, string userEmail, bool isAdmin, CancellationToken ct = default)
        => _t.SendAsync<SeafGroupMember>(HttpMethod.Put, $"{Base(groupId)}members/{Url.Seg(userEmail)}/",
            SeafTransport.Form(("is_admin", isAdmin ? "true" : "false")), ct);

    /// <summary>Lists libraries shared with a group.</summary>
    public async Task<IReadOnlyList<SeafAdminGroupLibrary>> ListLibrariesAsync(int groupId, CancellationToken ct = default)
        => (await _t.GetAsync<LibrariesResponse>(Base(groupId) + "libraries/", ct).ConfigureAwait(false)).Libraries;

    /// <summary>Stops sharing a library with a group.</summary>
    public Task RemoveLibraryAsync(int groupId, string libraryId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, $"{Base(groupId)}libraries/{Url.Seg(libraryId)}/", null, ct);

    private sealed record GroupListResponse { public List<SeafAdminGroup> Groups { get; init; } = []; public SeafPageInfo? PageInfo { get; init; } }
    private sealed record SearchResponse { public List<SeafAdminGroup> GroupList { get; init; } = []; }
    private sealed record MembersResponse { public List<SeafGroupMember> Members { get; init; } = []; public int TotalCount { get; init; } }
    private sealed record LibrariesResponse { public List<SeafAdminGroupLibrary> Libraries { get; init; } = []; }
}
