using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>Groups the current user belongs to, and their members.</summary>
public sealed class GroupsApi
{
    private readonly SeafTransport _t;
    internal GroupsApi(SeafTransport t) => _t = t;

    public async Task<IReadOnlyList<SeafGroup>> ListAsync(CancellationToken ct = default)
        => await _t.GetAsync<List<SeafGroup>>("api/v2.1/groups/", ct).ConfigureAwait(false);

    public Task<SeafGroup> GetAsync(int groupId, CancellationToken ct = default)
        => _t.GetAsync<SeafGroup>($"api/v2.1/groups/{groupId}/", ct);

    public Task<SeafGroup> CreateAsync(string name, CancellationToken ct = default)
        => _t.SendAsync<SeafGroup>(HttpMethod.Post, "api/v2.1/groups/", SeafTransport.Form(("name", name)), ct);

    public Task<SeafGroup> RenameAsync(int groupId, string newName, CancellationToken ct = default)
        => _t.SendAsync<SeafGroup>(HttpMethod.Put, $"api/v2.1/groups/{groupId}/", SeafTransport.Form(("name", newName)), ct);

    public Task DeleteAsync(int groupId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, $"api/v2.1/groups/{groupId}/", null, ct);

    public async Task<IReadOnlyList<SeafGroupMember>> ListMembersAsync(int groupId, CancellationToken ct = default)
        => await _t.GetAsync<List<SeafGroupMember>>($"api/v2.1/groups/{groupId}/members/", ct).ConfigureAwait(false);

    public Task<SeafGroupMember> AddMemberAsync(int groupId, string userEmail, CancellationToken ct = default)
        => _t.SendAsync<SeafGroupMember>(HttpMethod.Post, $"api/v2.1/groups/{groupId}/members/", SeafTransport.Form(("email", userEmail)), ct);

    /// <summary>Adds several members at once; per-user failures are reported in the result rather than thrown.</summary>
    public Task<SeafBulkMemberResult> AddMembersAsync(int groupId, IEnumerable<string> userEmails, CancellationToken ct = default)
        => _t.SendAsync<SeafBulkMemberResult>(HttpMethod.Post, $"api/v2.1/groups/{groupId}/members/bulk/",
            SeafTransport.Form(("emails", string.Join(',', userEmails))), ct);

    public Task RemoveMemberAsync(int groupId, string userEmail, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, $"api/v2.1/groups/{groupId}/members/{Url.Seg(userEmail)}/", null, ct);

    /// <summary>Promotes or demotes a group member to/from group admin.</summary>
    public Task<SeafGroupMember> SetAdminAsync(int groupId, string userEmail, bool isAdmin, CancellationToken ct = default)
        => _t.SendAsync<SeafGroupMember>(HttpMethod.Put, $"api/v2.1/groups/{groupId}/members/{Url.Seg(userEmail)}/",
            SeafTransport.Form(("is_admin", isAdmin ? "true" : "false")), ct);

    /// <summary>Lists libraries shared with a group.</summary>
    public async Task<IReadOnlyList<SeafLibrary>> ListLibrariesAsync(int groupId, CancellationToken ct = default)
        => await _t.GetAsync<List<SeafLibrary>>($"api/v2.1/groups/{groupId}/libraries/", ct).ConfigureAwait(false);

    /// <summary>Gets one member of a group.</summary>
    public Task<SeafGroupMember> GetMemberAsync(int groupId, string userEmail, CancellationToken ct = default)
        => _t.GetAsync<SeafGroupMember>($"api/v2.1/groups/{groupId}/members/{Url.Seg(userEmail)}/", ct);

    /// <summary>Gets one member of a group, or null if the user is not a member (or does not exist).</summary>
    public async Task<SeafGroupMember?> TryGetMemberAsync(int groupId, string userEmail, CancellationToken ct = default)
    {
        try { return await GetMemberAsync(groupId, userEmail, ct).ConfigureAwait(false); }
        // Seafile answers 404 for a non-member and 400 "Email ... invalid." for an unknown user.
        catch (SeafException e) when (e.IsNotFound || e.StatusCode == System.Net.HttpStatusCode.BadRequest) { return null; }
    }

    /// <summary>Transfers ownership of a group to another user. Only the owner may do this.</summary>
    /// <remarks>
    /// Seafile 14 does not check that <paramref name="newOwnerEmail"/> is an existing user: transferring to a typo succeeds
    /// and leaves the group ownerless for everyone but system admins. Verify the user first.
    /// </remarks>
    public Task<SeafGroup> TransferAsync(int groupId, string newOwnerEmail, CancellationToken ct = default)
        => _t.SendAsync<SeafGroup>(HttpMethod.Put, $"api/v2.1/groups/{groupId}/", SeafTransport.Form(("owner", newOwnerEmail)), ct);
}
