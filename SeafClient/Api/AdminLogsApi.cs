using System.Globalization;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>
/// Audit logs (Seafile Pro). The paginated <c>List*</c> methods use the current endpoints; the <c>Get*Async(start, end)</c>
/// methods use the older date-range endpoints, which return every event in the range in one response.
/// </summary>
public sealed class AdminLogsApi
{
    private readonly SeafTransport _t;
    internal AdminLogsApi(SeafTransport t) => _t = t;

    private const string Base = "api/v2.1/admin/logs/";

    /// <summary>Lists login attempts, newest first, optionally only for some users.</summary>
    public async Task<SeafPage<SeafLoginLog>> ListLoginsAsync(int page = 1, int perPage = 100, IEnumerable<string>? emails = null, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<LoginResponse>(Paged("login-logs/", page, perPage, ("email", emails)), ct).ConfigureAwait(false);
        return new SeafPage<SeafLoginLog> { Items = r.LoginLogList, Page = page, PerPage = perPage, TotalCount = r.TotalCount, HasNextPage = page * perPage < r.TotalCount };
    }

    /// <summary>Lists file access events (downloads, views), newest first.</summary>
    public async Task<SeafPage<SeafFileAccessLog>> ListFileAccessAsync(int page = 1, int perPage = 100, IEnumerable<string>? emails = null, IEnumerable<string>? libraryIds = null, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<FileAccessResponse>(Paged("file-access-logs/", page, perPage, ("email", emails), ("repo", libraryIds)), ct).ConfigureAwait(false);
        return new SeafPage<SeafFileAccessLog> { Items = r.FileAccessLogList, Page = page, PerPage = perPage, HasNextPage = r.HasNextPage };
    }

    /// <summary>Lists file changes, newest first.</summary>
    public async Task<SeafPage<SeafFileUpdateLog>> ListFileUpdatesAsync(int page = 1, int perPage = 100, IEnumerable<string>? emails = null, IEnumerable<string>? libraryIds = null, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<FileUpdateResponse>(Paged("file-update-logs/", page, perPage, ("email", emails), ("repo", libraryIds)), ct).ConfigureAwait(false);
        return new SeafPage<SeafFileUpdateLog> { Items = r.FileUpdateLogList, Page = page, PerPage = perPage, HasNextPage = r.HasNextPage };
    }

    /// <summary>Lists share permission changes, newest first.</summary>
    public async Task<SeafPage<SeafSharePermissionLog>> ListSharePermissionsAsync(int page = 1, int perPage = 100, IEnumerable<string>? fromEmails = null, IEnumerable<string>? toEmails = null, IEnumerable<string>? libraryIds = null, CancellationToken ct = default)
    {
        var r = await _t.GetAsync<SharePermissionResponse>(Paged("share-permission-logs/", page, perPage, ("from_email", fromEmails), ("to_email", toEmails), ("repo", libraryIds)), ct).ConfigureAwait(false);
        return new SeafPage<SeafSharePermissionLog> { Items = r.SharePermissionLogList, Page = page, PerPage = perPage, HasNextPage = r.HasNextPage };
    }

    // ---- date-range endpoints (whole days, server time) ----

    /// <summary>Login attempts between two dates (inclusive).</summary>
    public async Task<IReadOnlyList<SeafLoginLog>> GetLoginsAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
        => await _t.GetAsync<List<SeafLoginLog>>(Range("login/", start, end), ct).ConfigureAwait(false);

    /// <summary>File access events between two dates (inclusive).</summary>
    public async Task<IReadOnlyList<SeafFileAuditEvent>> GetFileAccessAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
        => await _t.GetAsync<List<SeafFileAuditEvent>>(Range("file-audit/", start, end), ct).ConfigureAwait(false);

    /// <summary>File changes between two dates (inclusive).</summary>
    public async Task<IReadOnlyList<SeafFileUpdateEvent>> GetFileUpdatesAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
        => await _t.GetAsync<List<SeafFileUpdateEvent>>(Range("file-update/", start, end), ct).ConfigureAwait(false);

    /// <summary>Permission changes between two dates (inclusive).</summary>
    public async Task<IReadOnlyList<SeafPermAuditEvent>> GetPermissionChangesAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
        => await _t.GetAsync<List<SeafPermAuditEvent>>(Range("perm-audit/", start, end), ct).ConfigureAwait(false);

    private static string Paged(string path, int page, int perPage, params (string Key, IEnumerable<string>? Values)[] filters)
    {
        var url = Url.Of(Base + path).Q("page", page).Q("per_page", perPage);
        foreach (var (key, values) in filters)
            foreach (var v in values ?? []) url.Q(key, v);
        return url;
    }

    private static string Range(string path, DateOnly start, DateOnly end)
        => Url.Of(Base + path).Q("start", start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Q("end", end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private sealed record LoginResponse { public List<SeafLoginLog> LoginLogList { get; init; } = []; public int TotalCount { get; init; } }
    private sealed record FileAccessResponse { public List<SeafFileAccessLog> FileAccessLogList { get; init; } = []; public bool HasNextPage { get; init; } }
    private sealed record FileUpdateResponse { public List<SeafFileUpdateLog> FileUpdateLogList { get; init; } = []; public bool HasNextPage { get; init; } }
    private sealed record SharePermissionResponse { public List<SeafSharePermissionLog> SharePermissionLogList { get; init; } = []; public bool HasNextPage { get; init; } }
}
