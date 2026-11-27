using System.Globalization;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>User management via the admin API. Requires a staff (system admin) account.</summary>
public sealed class AdminUsersApi
{
    private readonly SeafTransport _t;
    internal AdminUsersApi(SeafTransport t) => _t = t;

    public async Task<SeafPage<SeafUser>> ListAsync(int page = 1, int perPage = 100, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<UserListResponse>(Url.Of("api/v2.1/admin/users/").Q("page", page).Q("per_page", perPage), ct).ConfigureAwait(false);
        // Seafile 14 applies the page offset correctly but may return more than per_page rows; trim so pages don't overlap.
        var items = r.Data.Count > perPage ? r.Data.GetRange(0, perPage) : r.Data;
        return new SeafPage<SeafUser> { Items = items, TotalCount = r.TotalCount, Page = page, PerPage = perPage };
    }

    /// <summary>Enumerates all users, fetching pages lazily.</summary>
    public async IAsyncEnumerable<SeafUser> ListAllAsync(int perPage = 100, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var page = 1; ; page++)
        {
            var result = await ListAsync(page, perPage, ct).ConfigureAwait(false);
            foreach (var u in result.Items) yield return u;
            if (!result.HasNextPage || result.Items.Count == 0) yield break;
        }
    }

    /// <summary>Searches users by email, name or contact email.</summary>
    public async Task<IReadOnlyList<SeafUser>> SearchAsync(string query, CancellationToken ct = default)
        => (await _t.GetAsync<SearchResponse>(Url.Of("api/v2.1/admin/search-user/").Q("query", query), ct).ConfigureAwait(false)).UserList;

    public Task<SeafUser> GetAsync(string email, CancellationToken ct = default)
        => _t.GetAsync<SeafUser>($"api/v2.1/admin/users/{Url.Seg(email)}/", ct);

    public async Task<SeafUser?> TryGetAsync(string email, CancellationToken ct = default)
    {
        try { return await GetAsync(email, ct).ConfigureAwait(false); }
        catch (SeafException e) when (e.IsNotFound) { return null; }
    }

    public Task<SeafUser> CreateAsync(SeafCreateUserRequest request, CancellationToken ct = default)
        => _t.SendAsync<SeafUser>(HttpMethod.Post, "api/v2.1/admin/users/", SeafTransport.Form(
            ("email", request.Email),
            ("password", request.Password),
            ("name", request.Name),
            ("contact_email", request.ContactEmail),
            ("login_id", request.LoginId),
            ("role", request.Role),
            ("is_staff", Bool(request.IsStaff)),
            ("is_active", Bool(request.IsActive)),
            ("quota_total", request.QuotaTotalMb?.ToString(CultureInfo.InvariantCulture))), ct);

    public Task<SeafUser> UpdateAsync(string email, SeafUpdateUserRequest request, CancellationToken ct = default)
        => _t.SendAsync<SeafUser>(HttpMethod.Put, $"api/v2.1/admin/users/{Url.Seg(email)}/", SeafTransport.Form(
            ("name", request.Name),
            ("password", request.Password),
            ("contact_email", request.ContactEmail),
            ("login_id", request.LoginId),
            ("role", request.Role),
            ("institution", request.Institution),
            ("is_staff", Bool(request.IsStaff)),
            ("is_active", Bool(request.IsActive)),
            ("quota_total", request.QuotaTotalMb?.ToString(CultureInfo.InvariantCulture))), ct);

    public Task DeleteAsync(string email, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, $"api/v2.1/admin/users/{Url.Seg(email)}/", null, ct);

    /// <summary>Lists the libraries a user has shared with other users and groups.</summary>
    public async Task<IReadOnlyList<SeafUserSharedRepo>> ListSharedLibrariesAsync(string email, CancellationToken ct = default)
        => await _t.GetAsync<List<SeafUserSharedRepo>>($"api/v2.1/admin/users/{Url.Seg(email)}/shared-repos/", ct).ConfigureAwait(false);

    /// <summary>Lists the folders (inside libraries) a user has shared with other users and groups.</summary>
    public async Task<IReadOnlyList<SeafUserSharedFolder>> ListSharedFoldersAsync(string email, CancellationToken ct = default)
        => await _t.GetAsync<List<SeafUserSharedFolder>>($"api/v2.1/admin/users/{Url.Seg(email)}/shared-folders/", ct).ConfigureAwait(false);

    private static string? Bool(bool? b) => b == null ? null : b.Value ? "true" : "false";

    private sealed record UserListResponse { public List<SeafUser> Data { get; init; } = []; public int TotalCount { get; init; } }
    private sealed record SearchResponse { public List<SeafUser> UserList { get; init; } = []; }
}
