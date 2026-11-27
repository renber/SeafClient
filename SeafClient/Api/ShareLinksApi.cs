using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>Public share links and internal smart links.</summary>
public sealed partial class ShareLinksApi
{
    private readonly SeafTransport _t;
    internal ShareLinksApi(SeafTransport t) => _t = t;

    /// <summary>
    /// Lists the current user's share links, optionally filtered to a library and a path in it.
    /// The server matches <paramref name="path"/> exactly (directories with or without a trailing '/').
    /// </summary>
    public async Task<IReadOnlyList<SeafShareLink>> ListAsync(string? libraryId = null, string? path = null, CancellationToken ct = default)
    {
        if (path != null && libraryId == null) throw new ArgumentException("A path filter requires a library id.", nameof(path));
        return await _t.GetAsync<List<SeafShareLink>>(Url.Of("api/v2.1/share-links/")
            .Q("repo_id", libraryId).Q("path", path == null ? null : SeafPath.Normalize(path)), ct).ConfigureAwait(false);
    }

    public Task<SeafShareLink> GetAsync(string token, CancellationToken ct = default)
        => _t.GetAsync<SeafShareLink>($"api/v2.1/share-links/{Url.Seg(token)}/", ct);

    /// <summary>Creates a share link for a file or directory.</summary>
    public Task<SeafShareLink> CreateAsync(string libraryId, string path, SeafShareLinkOptions? options = null, CancellationToken ct = default)
    {
        options ??= new SeafShareLinkOptions();
        return _t.SendAsync<SeafShareLink>(HttpMethod.Post, "api/v2.1/share-links/", SeafTransport.Json(new
        {
            repo_id = libraryId,
            path = SeafPath.Normalize(path),
            password = options.Password,
            expiration_time = options.ExpiresAt?.ToString("yyyy-MM-dd'T'HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture),
            expire_days = options.ExpiresAt == null ? options.ExpireDays : null,
            permissions = new { can_edit = options.CanEdit, can_download = options.CanDownload, can_upload = options.CanUpload },
        }), ct);
    }

    /// <summary>
    /// Returns an unexpired share link for the item if one exists, otherwise creates one. Expired (or stale, when
    /// <see cref="SeafShareLinkOptions.MinRemainingLifetime"/> is set) links for the item are deleted.
    /// </summary>
    /// <returns>The link and whether it was newly created.</returns>
    public async Task<(SeafShareLink Link, bool Created)> GetOrCreateAsync(string libraryId, string path, SeafShareLinkOptions? options = null, CancellationToken ct = default)
    {
        var existing = await ListAsync(libraryId, path, ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var minLifetime = options?.MinRemainingLifetime;

        foreach (var stale in existing.Where(l => IsStale(l, now, minLifetime)))
            await DeleteAsync(stale.Token, ct).ConfigureAwait(false);

        var valid = existing.Where(l => !IsStale(l, now, minLifetime)).OrderByDescending(l => l.Created).FirstOrDefault();
        if (valid != null)
        {
            if (options is { RequireMatchingPermissions: true })
            {
                var wantsPassword = !string.IsNullOrEmpty(options.Password);
                if (valid.HasPassword != wantsPassword)
                {
                    await DeleteAsync(valid.Token, ct).ConfigureAwait(false);
                    return (await CreateAsync(libraryId, path, options, ct).ConfigureAwait(false), true);
                }

                var p = valid.Permissions;
                if (p.CanEdit != options.CanEdit || p.CanDownload != options.CanDownload || p.CanUpload != options.CanUpload)
                {
                    var updated = await UpdateAsync(valid.Token, new SeafShareLinkUpdate
                    {
                        CanEdit = options.CanEdit,
                        CanDownload = options.CanDownload,
                        CanUpload = options.CanUpload,
                        ExpiresAt = options.ExpiresAt,
                        ExpireDays = options.ExpireDays,
                    }, ct).ConfigureAwait(false);
                    return (updated, false);
                }
            }
            return (valid, false);
        }

        return (await CreateAsync(libraryId, path, options, ct).ConfigureAwait(false), true);

        static bool IsStale(SeafShareLink l, DateTimeOffset utcNow, TimeSpan? min)
            => l.IsExpiredNow || (min is { } m && l.ExpiresAt is { } exp && exp - utcNow < m);
    }

    public Task DeleteAsync(string token, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, $"api/v2.1/share-links/{Url.Seg(token)}/", null, ct);

    /// <summary>Returns an internal link to a file or directory that only works for users with access to it, and keeps working after renames.</summary>
    public Task<SeafSmartLink> GetSmartLinkAsync(string libraryId, string path, bool isDirectory, CancellationToken ct = default)
        => _t.GetAsync<SeafSmartLink>(Url.Of("api/v2.1/smart-link/")
            .Q("repo_id", libraryId).Q("path", SeafPath.Normalize(path)).Q("is_dir", isDirectory), ct);
}
