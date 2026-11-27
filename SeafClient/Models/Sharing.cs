using System.Text.Json.Serialization;

namespace SeafClient.Models;

/// <summary>A public share link (<c>/api/v2.1/share-links/</c>).</summary>
public sealed record SeafShareLink
{
    /// <summary>The link token; use it to delete or fetch the link.</summary>
    public string Token { get; init; } = "";

    /// <summary>The URL to share, e.g. <c>https://host/d/abc123/</c>.</summary>
    public string Link { get; init; } = "";

    /// <summary>Direct download URL (Seafile 13+).</summary>
    public string? DownloadLink { get; init; }

    [JsonPropertyName("repo_id")] public string LibraryId { get; init; } = "";
    [JsonPropertyName("repo_name")] public string? LibraryName { get; init; }

    /// <summary>Path of the shared item. Directory paths end with '/'.</summary>
    public string Path { get; init; } = "";

    public string ObjName { get; init; } = "";
    public bool IsDir { get; init; }
    public string? Username { get; init; }

    [JsonPropertyName("ctime")] public DateTimeOffset? Created { get; init; }

    /// <summary>Null when the link never expires.</summary>
    [JsonPropertyName("expire_date")] public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Expiry as evaluated by the server when the link was fetched.</summary>
    public bool IsExpired { get; init; }

    [JsonPropertyName("view_cnt")] public int ViewCount { get; init; }

    public SeafShareLinkPermissions Permissions { get; init; } = new();

    /// <summary>The link password, returned to the link owner only. Empty when the link has none.</summary>
    public string? Password { get; init; }

    public string? UserScope { get; init; }
    public string? Description { get; init; }

    /// <summary>True if the link is expired according to the server, or its expiry has passed since it was fetched.</summary>
    [JsonIgnore]
    public bool IsExpiredNow => IsExpired || (ExpiresAt is { } e && e <= DateTimeOffset.UtcNow);

    [JsonIgnore]
    public bool HasPassword => !string.IsNullOrEmpty(Password);
}

public sealed record SeafShareLinkPermissions
{
    public bool CanEdit { get; init; }
    public bool CanDownload { get; init; } = true;
    public bool CanUpload { get; init; }
}

/// <summary>Options for <see cref="Api.ShareLinksApi.CreateAsync"/>.</summary>
public sealed class SeafShareLinkOptions
{
    /// <summary>Optional link password. Seafile enforces a minimum length (8 by default).</summary>
    public string? Password { get; set; }

    /// <summary>Absolute expiry. Takes precedence over <see cref="ExpireDays"/>.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Expiry in whole days from now.</summary>
    public int? ExpireDays { get; set; }

    public bool CanEdit { get; set; }
    public bool CanDownload { get; set; } = true;

    /// <summary>Allow uploads through the link (directories only).</summary>
    public bool CanUpload { get; set; }

    /// <summary>
    /// When true, <see cref="Api.ShareLinksApi.GetOrCreateAsync"/> verifies that an existing link's permissions and
    /// password presence match these options (updating or recreating the link if not).
    /// </summary>
    public bool RequireMatchingPermissions { get; set; }

    /// <summary>
    /// When set, <see cref="Api.ShareLinksApi.GetOrCreateAsync"/> treats any existing link with less than this
    /// remaining lifetime as expired, deletes it, and creates a fresh link.
    /// </summary>
    public TimeSpan? MinRemainingLifetime { get; set; }
}

/// <summary>An internal "smart link" that survives renames/moves (only works for logged-in users).</summary>
public sealed record SeafSmartLink
{
    public string SmartLink { get; init; } = "";
    public string SmartLinkToken { get; init; } = "";
    public string Name { get; init; } = "";
}

/// <summary>Permission granted when sharing a library or folder with a user or group.</summary>
public enum SeafSharePermission
{
    /// <summary>Read only.</summary>
    R,
    /// <summary>Read and write.</summary>
    Rw,
    /// <summary>Admin (users and groups in Seafile Pro).</summary>
    Admin,
}

/// <summary>A user or group that a library folder has been shared with.</summary>
public sealed record SeafSharedItem
{
    /// <summary><c>user</c> or <c>group</c>.</summary>
    public string ShareType { get; init; } = "";

    public string Permission { get; init; } = "";
    public bool IsAdmin { get; init; }

    public SeafSharedUser? UserInfo { get; init; }
    public SeafSharedGroup? GroupInfo { get; init; }
}

public sealed record SeafSharedUser
{
    public string Name { get; init; } = "";
    public string Nickname { get; init; } = "";
}

public sealed record SeafSharedGroup
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}
