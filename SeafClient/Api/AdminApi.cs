using SeafClient.Internal;

namespace SeafClient.Api;

/// <summary>
/// System administration endpoints (<c>/api/v2.1/admin/</c>). Requires a staff (system admin) account; some features
/// (logs, departments, organizations) also require Seafile Pro and the matching server settings.
/// </summary>
public sealed class AdminApi
{
    internal AdminApi(SeafTransport t)
    {
        Users = new AdminUsersApi(t);
        Groups = new AdminGroupsApi(t);
        Libraries = new AdminLibrariesApi(t);
        Departments = new AdminDepartmentsApi(t);
        Logs = new AdminLogsApi(t);
        Organizations = new AdminOrganizationsApi(t);
        Shares = new AdminSharesApi(t);
        ShareLinks = new AdminShareLinksApi(t);
        Notifications = new AdminNotificationsApi(t);
    }

    /// <summary>User accounts.</summary>
    public AdminUsersApi Users { get; }

    public AdminGroupsApi Groups { get; }
    public AdminLibrariesApi Libraries { get; }
    public AdminDepartmentsApi Departments { get; }
    public AdminLogsApi Logs { get; }
    public AdminOrganizationsApi Organizations { get; }
    public AdminSharesApi Shares { get; }
    public AdminShareLinksApi ShareLinks { get; }
    public AdminNotificationsApi Notifications { get; }
}

/// <summary><c>page_info</c> block returned by several admin list endpoints.</summary>
internal sealed record SeafPageInfo
{
    public bool HasNextPage { get; init; }
    public int CurrentPage { get; init; }
}
