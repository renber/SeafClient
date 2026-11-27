using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeafClient.Models;

// ---------------- groups ----------------

/// <summary>A group as seen by the admin API.</summary>
public sealed record SeafAdminGroup
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Owner { get; init; }
    public string? OwnerName { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? Created { get; init; }

    /// <summary>Quota in bytes; -1 unlimited, -2 not set (departments only).</summary>
    public long Quota { get; init; }

    /// <summary>0 for a normal group, -1 for a top-level department, otherwise the parent department id.</summary>
    public int ParentGroupId { get; init; }

    [JsonIgnore] public bool IsDepartment => ParentGroupId != 0;
}

/// <summary>A library shared with a group, as listed by the admin API.</summary>
public sealed record SeafAdminGroupLibrary
{
    [JsonPropertyName("repo_id")] public string LibraryId { get; init; } = "";
    public string Name { get; init; } = "";
    public long Size { get; init; }
    public string? SharedBy { get; init; }
    public string? SharedByName { get; init; }
    public string Permission { get; init; } = "";
    public int GroupId { get; init; }
    public bool Encrypted { get; init; }
}

// ---------------- departments ----------------

/// <summary>A department (address-book group).</summary>
public sealed record SeafDepartment
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Owner { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? Created { get; init; }

    /// <summary>-1 for a top-level department.</summary>
    public int ParentGroupId { get; init; }

    /// <summary>Quota in bytes; -2 means not set.</summary>
    public long Quota { get; init; }

    public int OrgId { get; init; }
}

/// <summary>A department with its child departments and members.</summary>
public sealed record SeafDepartmentDetail
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Owner { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? Created { get; init; }
    public int ParentGroupId { get; init; }
    public long Quota { get; init; }
    public int OrgId { get; init; }

    public IReadOnlyList<SeafDepartment> Groups { get; init; } = [];
    public IReadOnlyList<SeafGroupMember> Members { get; init; } = [];

    /// <summary>Parent chain (root first), only filled when requested.</summary>
    public IReadOnlyList<SeafDepartment> AncestorGroups { get; init; } = [];
}

// ---------------- libraries ----------------

/// <summary>A library as seen by the admin API.</summary>
public sealed record SeafAdminLibrary
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Owner { get; init; }
    public string? OwnerEmail { get; init; }
    public string? OwnerName { get; init; }
    public string? OwnerContactEmail { get; init; }
    public long Size { get; init; }
    public bool Encrypted { get; init; }
    public long FileCount { get; init; }

    /// <summary><c>normal</c> or <c>read-only</c>.</summary>
    public string? Status { get; init; }

    public DateTimeOffset? LastModified { get; init; }
}

/// <summary>A deleted library in the system trash.</summary>
public sealed record SeafTrashLibrary
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Owner { get; init; }
    public string? OwnerName { get; init; }
    [JsonPropertyName("delete_time")] public DateTimeOffset? Deleted { get; init; }
}

/// <summary>A file or folder created through the admin library-dirents API.</summary>
public sealed record SeafAdminDirent
{
    public bool IsFile { get; init; }
    public string ObjName { get; init; } = "";
    public string? FileSize { get; init; }
    public DateTimeOffset? LastUpdate { get; init; }
}

/// <summary>Library status for <see cref="Api.AdminLibrariesApi.SetStatusAsync"/>.</summary>
public enum SeafLibraryStatus
{
    /// <summary><c>normal</c></summary>
    Normal,
    /// <summary><c>read-only</c></summary>
    ReadOnly,
}

// ---------------- shares ----------------

/// <summary>A library share as reported by the admin shares API.</summary>
public sealed record SeafAdminShare
{
    [JsonPropertyName("repo_id")] public string LibraryId { get; init; } = "";
    public string Path { get; init; } = "/";

    /// <summary><c>user</c> or <c>group</c>.</summary>
    public string ShareType { get; init; } = "";

    public string Permission { get; init; } = "";
    public bool IsAdmin { get; init; }

    public string? UserEmail { get; init; }
    public string? UserName { get; init; }
    public int? GroupId { get; init; }
    public string? GroupName { get; init; }
}

/// <summary>Result of sharing a library with several users/groups at once.</summary>
public sealed record SeafAdminShareResult
{
    public IReadOnlyList<SeafAdminShare> Success { get; init; } = [];
    public IReadOnlyList<SeafAdminShareFailure> Failed { get; init; } = [];
}

public sealed record SeafAdminShareFailure
{
    public string? Email { get; init; }
    public string? GroupName { get; init; }
    public int? GroupId { get; init; }
    public string ErrorMsg { get; init; } = "";
}

/// <summary>A folder a user shared, from <c>admin/users/{email}/shared-folders/</c>.</summary>
public sealed record SeafUserSharedFolder
{
    [JsonPropertyName("repo_id")] public string LibraryId { get; init; } = "";
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }
    public string Path { get; init; } = "";
    public string? FolderName { get; init; }

    /// <summary><c>personal</c> or <c>group</c>.</summary>
    public string ShareType { get; init; } = "";

    public string SharePermission { get; init; } = "";
    public string? ShareFrom { get; init; }
    public string? ShareFromUserName { get; init; }
    public string? UserEmail { get; init; }
    public string? UserName { get; init; }
    public string? ContactEmail { get; init; }
    public int? GroupId { get; init; }
    public string? GroupName { get; init; }
}

/// <summary>A library a user shared, from <c>admin/users/{email}/shared-repos/</c>.</summary>
public sealed record SeafUserSharedRepo
{
    [JsonPropertyName("repo_id")] public string LibraryId { get; init; } = "";
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }
    public bool Encrypted { get; init; }

    /// <summary><c>personal</c> or <c>group</c>.</summary>
    public string ShareType { get; init; } = "";

    public string SharePermission { get; init; } = "";
    public string? ModifierEmail { get; init; }
    public string? ModifierName { get; init; }
    public string? ShareFrom { get; init; }
    public string? ShareFromUserName { get; init; }
    public string? UserEmail { get; init; }
    public string? UserName { get; init; }
    public string? ContactEmail { get; init; }
    public int? GroupId { get; init; }
    public string? GroupName { get; init; }
}

// ---------------- share links ----------------

/// <summary>A share link as seen by the admin API. The list endpoint fills only a subset of the fields.</summary>
public sealed record SeafAdminShareLink
{
    public string Token { get; init; } = "";
    public string? Link { get; init; }
    public string ObjName { get; init; } = "";
    public string? Path { get; init; }
    public bool IsDir { get; init; }
    [JsonPropertyName("repo_id")] public string? LibraryId { get; init; }
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }
    public string? CreatorEmail { get; init; }
    public string? CreatorName { get; init; }
    public string? CreatorContactEmail { get; init; }
    [JsonPropertyName("ctime")] public DateTimeOffset? Created { get; init; }
    [JsonPropertyName("expire_date")] public DateTimeOffset? ExpiresAt { get; init; }
    public bool IsExpired { get; init; }
    [JsonPropertyName("view_cnt")] public int ViewCount { get; init; }
    public SeafShareLinkPermissions? Permissions { get; init; }
}

/// <summary>An entry inside a shared folder link.</summary>
public sealed record SeafAdminShareLinkDirent
{
    public string ObjName { get; init; } = "";
    public string Path { get; init; } = "";
    public bool IsDir { get; init; }
    public long Size { get; init; }
    public DateTimeOffset? LastModified { get; init; }
}

// ---------------- notifications ----------------

/// <summary>A user notification, as listed by the admin API. <see cref="Detail"/> depends on <see cref="Type"/>.</summary>
public sealed record SeafAdminNotification
{
    public long Id { get; init; }
    public string Type { get; init; } = "";
    public DateTimeOffset? Time { get; init; }
    public JsonElement Detail { get; init; }
}

// ---------------- organizations ----------------

/// <summary>An organization (multi-tenancy must be enabled on the server).</summary>
public sealed record SeafOrganization
{
    public int OrgId { get; init; }
    public string OrgName { get; init; } = "";
    [JsonPropertyName("ctime")] public DateTimeOffset? Created { get; init; }
    public string? OrgUrlPrefix { get; init; }
    public string? Role { get; init; }
    public bool IsActive { get; init; }
    public string? CreatorEmail { get; init; }
    public string? CreatorName { get; init; }
    public string? CreatorContactEmail { get; init; }

    /// <summary>Quota in bytes; negative means unlimited.</summary>
    public long Quota { get; init; }

    public long QuotaUsage { get; init; }
    public int? MaxUserNumber { get; init; }
}

/// <summary>Fields for creating an organization.</summary>
public sealed class SeafCreateOrganizationRequest
{
    public required string OrgName { get; set; }
    public required string OwnerEmail { get; set; }
    public required string OwnerPassword { get; set; }

    /// <summary>Quota in MB.</summary>
    public long? QuotaMb { get; set; }

    public int? MemberLimit { get; set; }
}

/// <summary>Organization fields to change; null fields are left untouched.</summary>
public sealed class SeafUpdateOrganizationRequest
{
    public string? OrgName { get; set; }
    public int? MaxUserNumber { get; set; }

    /// <summary>Quota in MB.</summary>
    public long? QuotaMb { get; set; }

    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

// ---------------- logs (paginated, Seafile 11+) ----------------

/// <summary>A login attempt (<c>admin/logs/login-logs/</c>).</summary>
public sealed record SeafLoginLog
{
    public DateTimeOffset? LoginTime { get; init; }
    public string? LoginIp { get; init; }

    /// <summary>Whether the login succeeded (null on the legacy date-range endpoint).</summary>
    [JsonPropertyName("log_success")] public bool? Success { get; init; }

    public string Email { get; init; } = "";
    public string? Name { get; init; }
    public string? ContactEmail { get; init; }
}

/// <summary>A file access event (<c>admin/logs/file-access-logs/</c>).</summary>
public sealed record SeafFileAccessLog
{
    public string Email { get; init; } = "";
    public string? Name { get; init; }
    public string? ContactEmail { get; init; }
    public string? Ip { get; init; }

    /// <summary>e.g. <c>API</c>, <c>web</c>, <c>share-link</c>.</summary>
    public string? EventType { get; init; }

    public string? Device { get; init; }
    public DateTimeOffset? Time { get; init; }
    [JsonPropertyName("repo_id")] public string? LibraryId { get; init; }
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }
    public string? FileOrDirName { get; init; }
}

/// <summary>A file change (<c>admin/logs/file-update-logs/</c>).</summary>
public sealed record SeafFileUpdateLog
{
    public string Email { get; init; } = "";
    public string? Name { get; init; }
    public string? ContactEmail { get; init; }
    public DateTimeOffset? Time { get; init; }
    [JsonPropertyName("repo_id")] public string? LibraryId { get; init; }
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }
    [JsonPropertyName("repo_encrypted")] public bool LibraryEncrypted { get; init; }
    public string? FileOperation { get; init; }
    public string? CommitId { get; init; }
}

/// <summary>A share permission change (<c>admin/logs/share-permission-logs/</c>).</summary>
public sealed record SeafSharePermissionLog
{
    public string? FromUserEmail { get; init; }
    public string? FromUserName { get; init; }
    public string? FromUserContactEmail { get; init; }

    /// <summary><c>add-repo-perm</c>, <c>modify-repo-perm</c> or <c>delete-repo-perm</c>.</summary>
    public string? Etype { get; init; }

    public string? Permission { get; init; }
    [JsonPropertyName("repo_id")] public string? LibraryId { get; init; }
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }
    public string? Folder { get; init; }
    [JsonPropertyName("date")] public DateTimeOffset? Time { get; init; }

    /// <summary><c>user</c>, <c>group</c> or <c>all</c>.</summary>
    public string? ShareType { get; init; }

    public string? ToUserEmail { get; init; }
    public string? ToUserName { get; init; }
    public string? ToUserContactEmail { get; init; }
    public string? ToGroupId { get; init; }
    public string? ToGroupName { get; init; }
}

// ---------------- logs (legacy date-range endpoints) ----------------

/// <summary>A file access event from the date-range endpoint <c>admin/logs/file-audit/</c>.</summary>
public sealed record SeafFileAuditEvent
{
    [JsonPropertyName("repo_id")] public string? LibraryId { get; init; }
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }
    [JsonPropertyName("repo_owner_email")] public string? LibraryOwnerEmail { get; init; }
    [JsonPropertyName("repo_owner_name")] public string? LibraryOwnerName { get; init; }
    public DateTimeOffset? Time { get; init; }
    public string? Ip { get; init; }
    public string? FilePath { get; init; }

    /// <summary>e.g. <c>file-download-web</c>, <c>file-download-api</c>, <c>repo-download-sync</c>.</summary>
    public string? Etype { get; init; }

    public string? UserEmail { get; init; }
    public string? UserName { get; init; }
    public string? UserContactEmail { get; init; }
}

/// <summary>A file change from the date-range endpoint <c>admin/logs/file-update/</c>.</summary>
public sealed record SeafFileUpdateEvent
{
    [JsonPropertyName("repo_id")] public string? LibraryId { get; init; }
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }
    [JsonPropertyName("repo_owner_email")] public string? LibraryOwnerEmail { get; init; }
    [JsonPropertyName("repo_owner_name")] public string? LibraryOwnerName { get; init; }
    public string? CommitId { get; init; }
    public DateTimeOffset? Time { get; init; }
    public string? FileOperation { get; init; }
    public string? UserEmail { get; init; }
    public string? UserName { get; init; }
    public string? UserContactEmail { get; init; }
}

/// <summary>A permission change from the date-range endpoint <c>admin/logs/perm-audit/</c>.</summary>
public sealed record SeafPermAuditEvent
{
    public string? Etype { get; init; }
    [JsonPropertyName("repo_id")] public string? LibraryId { get; init; }
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }
    public string? Permission { get; init; }
    public DateTimeOffset? Time { get; init; }
    public string? FilePath { get; init; }
    public string? FromName { get; init; }
    public string? FromEmail { get; init; }

    /// <summary>The target: a user email, a group id, or <c>all</c>.</summary>
    public string? To { get; init; }
}
