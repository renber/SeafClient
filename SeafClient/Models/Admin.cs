using System.Text.Json.Serialization;

namespace SeafClient.Models;

/// <summary>A user as seen by the admin API (<c>/api/v2.1/admin/users/</c>).</summary>
public sealed record SeafUser
{
    /// <summary>The internal user id. For SSO / newer installs this is a virtual id like <c>xxxx@auth.local</c>.</summary>
    public string Email { get; init; } = "";

    public string Name { get; init; } = "";
    public string? ContactEmail { get; init; }
    public string? LoginId { get; init; }
    public string? AvatarUrl { get; init; }
    public bool IsStaff { get; init; }
    public bool IsActive { get; init; }
    public string? Role { get; init; }
    public string? Institution { get; init; }

    /// <summary>Bytes used.</summary>
    public long QuotaUsage { get; init; }

    /// <summary>Quota in bytes; negative means unlimited.</summary>
    public long QuotaTotal { get; init; }

    [JsonPropertyName("create_time")] public DateTimeOffset? Created { get; init; }
    public DateTimeOffset? LastLogin { get; init; }
    public DateTimeOffset? LastAccessTime { get; init; }
}

/// <summary>A page of results from a paginated admin endpoint.</summary>
public sealed record SeafPage<T>
{
    private readonly bool? _hasNextPage;

    public IReadOnlyList<T> Items { get; init; } = [];
    public int? TotalCount { get; init; }
    public int Page { get; init; }
    public int PerPage { get; init; }
    public bool HasNextPage { get => _hasNextPage ?? (TotalCount is { } total && Page * PerPage < total); init => _hasNextPage = value; }
}

/// <summary>Fields for creating a user via the admin API.</summary>
public sealed class SeafCreateUserRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? Name { get; set; }
    public string? ContactEmail { get; set; }
    public string? LoginId { get; set; }
    public string? Role { get; set; }
    public bool? IsStaff { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>Quota in MB.</summary>
    public long? QuotaTotalMb { get; set; }
}

/// <summary>Fields to change via the admin API; null fields are left untouched.</summary>
public sealed class SeafUpdateUserRequest
{
    public string? Name { get; set; }
    public string? Password { get; set; }
    public string? ContactEmail { get; set; }
    public string? LoginId { get; set; }
    public string? Role { get; set; }
    public string? Institution { get; set; }
    public bool? IsStaff { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>Quota in MB.</summary>
    public long? QuotaTotalMb { get; set; }
}
