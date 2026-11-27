using System.Text.Json;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api.RepoToken;

/// <summary>
/// Metadata of a repo-token library: records, views and tags (<c>/api/v2.1/via-repo-token/metadata/</c>).
/// Metadata must already be enabled on the library by a user (<see cref="MetadataApi.SetEnabledAsync"/>).
/// </summary>
/// <remarks>
/// On Seafile 14.0 some tag-link endpoints fail through a repo token (the server passes an anonymous user to the metadata
/// server): setting file tags reports every record in <see cref="SeafFileTagsResult.Fail"/>, <c>tag-files</c> returns
/// nothing and tag links return 500. Use the account API (<see cref="MetadataTagsApi"/>) for those.
/// </remarks>
public sealed class RepoTokenMetadataApi
{
    private readonly MetadataCore _core;

    /// <summary>Records per update request; overridable by tests to exercise multi-request batching.</summary>
    internal int UpdateBatchSize { get; set; } = MetadataApi.MaxBatchSize;

    internal RepoTokenMetadataApi(RepoTokenContext c)
    {
        _core = new MetadataCore(c.T, RepoTokenContext.Base + "metadata/");
        Views = new RepoTokenMetadataViewsApi(_core);
        Tags = new RepoTokenMetadataTagsApi(_core);
    }

    public RepoTokenMetadataViewsApi Views { get; }
    public RepoTokenMetadataTagsApi Tags { get; }

    /// <summary>Lists the records of a view with the view's filters and sorts applied.</summary>
    public Task<SeafMetadataQueryResult> ListRecordsAsync(string viewId, int start = 0, int limit = 1000, CancellationToken ct = default)
        => _core.ListRecordsAsync(viewId, start, limit, ct);

    /// <summary>Updates column values of one record (values keyed by column name).</summary>
    public Task UpdateRecordAsync(string recordId, IReadOnlyDictionary<string, object?> values, CancellationToken ct = default)
        => _core.UpdateRecordsAsync([new SeafMetadataUpdate(recordId, values)], UpdateBatchSize, ct);

    /// <summary>Updates many records, batched (records with different values are sent in separate requests).</summary>
    public Task UpdateRecordsAsync(IEnumerable<SeafMetadataUpdate> updates, CancellationToken ct = default)
        => _core.UpdateRecordsAsync(updates, UpdateBatchSize, ct);
}

/// <summary>Metadata views of a repo-token library.</summary>
public sealed class RepoTokenMetadataViewsApi
{
    private readonly MetadataCore _core;
    internal RepoTokenMetadataViewsApi(MetadataCore core) => _core = core;

    public Task<SeafMetadataViewList> ListAsync(CancellationToken ct = default) => _core.ListViewsAsync(ct);

    public Task<SeafMetadataView> GetAsync(string viewId, CancellationToken ct = default) => _core.GetViewAsync(viewId, ct);

    public Task<SeafMetadataView> CreateAsync(string name, string type = "table", string? folderId = null, object? data = null, CancellationToken ct = default)
        => _core.CreateViewAsync(name, type, folderId, data, ct);

    /// <summary>Merges <paramref name="viewData"/> (e.g. <c>new { name = "Docs" }</c>) into the view.</summary>
    public Task UpdateAsync(string viewId, object viewData, CancellationToken ct = default) => _core.UpdateViewAsync(viewId, viewData, ct);

    public Task RenameAsync(string viewId, string newName, CancellationToken ct = default) => _core.UpdateViewAsync(viewId, new { name = newName }, ct);

    public Task DeleteAsync(string viewId, string? folderId = null, CancellationToken ct = default) => _core.DeleteViewAsync(viewId, folderId, ct);

    public Task<SeafMetadataView> DuplicateAsync(string viewId, string? folderId = null, CancellationToken ct = default) => _core.DuplicateViewAsync(viewId, folderId, ct);

    public Task<IReadOnlyList<SeafMetadataNavigationItem>> MoveAsync(string? sourceViewId, string? targetViewId, string? targetFolderId = null, string? sourceFolderId = null, bool isAboveFolder = false, CancellationToken ct = default)
        => _core.MoveViewAsync(sourceViewId, sourceFolderId, targetViewId, targetFolderId, isAboveFolder, ct);
}

/// <summary>Tags of a repo-token library. See the remarks on <see cref="RepoTokenMetadataApi"/> about server limitations.</summary>
public sealed class RepoTokenMetadataTagsApi
{
    private readonly MetadataCore _core;
    internal RepoTokenMetadataTagsApi(MetadataCore core) => _core = core;

    public Task SetEnabledAsync(bool enabled, string lang = "en", CancellationToken ct = default) => _core.SetTagsEnabledAsync(enabled, lang, ct);

    public Task<IReadOnlyList<SeafMetadataTag>> ListAsync(int start = 0, int limit = 1000, CancellationToken ct = default) => _core.ListTagsAsync(start, limit, ct);

    /// <summary>Creates tags; existing names return the existing tag.</summary>
    public Task<IReadOnlyList<SeafMetadataTag>> CreateAsync(IEnumerable<SeafNewMetadataTag> tags, CancellationToken ct = default) => _core.CreateTagsAsync(tags, ct);

    public async Task<SeafMetadataTag> CreateAsync(string name, string? color = null, CancellationToken ct = default)
        => (await _core.CreateTagsAsync([new SeafNewMetadataTag(name, color)], ct).ConfigureAwait(false)).First();

    public Task UpdateAsync(IEnumerable<SeafMetadataTagUpdate> updates, CancellationToken ct = default) => _core.UpdateTagsAsync(updates, ct);

    public Task DeleteAsync(IEnumerable<string> tagIds, CancellationToken ct = default) => _core.DeleteTagsAsync(tagIds, ct);

    /// <summary>Replaces the tags of files (record id → tag ids). Unlike the account API, this reports success per record.</summary>
    public async Task<SeafFileTagsResult> SetFileTagsAsync(IReadOnlyDictionary<string, IEnumerable<string>> recordTags, CancellationToken ct = default)
    {
        var json = await _core.SetFileTagsAsync(recordTags, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.Array)
            return SeafJson.Deserialize<SeafFileTagsResult>(json);
        // Same shape as the account API ({"success": true}): everything succeeded.
        return new SeafFileTagsResult { Success = recordTags.Keys.ToList() };
    }

    public Task<SeafMetadataQueryResult> ListFilesAsync(string tagId, CancellationToken ct = default) => _core.ListTagFilesAsync(tagId, ct);

    public Task<SeafMetadataQueryResult> ListFilesAsync(IEnumerable<string> tagIds, CancellationToken ct = default) => _core.ListTagsFilesAsync(tagIds, ct);

    public Task AddLinksAsync(string linkColumnKey, IReadOnlyDictionary<string, IEnumerable<string>> rowIdMap, CancellationToken ct = default)
        => _core.ChangeTagLinksAsync(HttpMethod.Post, linkColumnKey, rowIdMap, ct);

    public Task SetLinksAsync(string linkColumnKey, IReadOnlyDictionary<string, IEnumerable<string>> rowIdMap, CancellationToken ct = default)
        => _core.ChangeTagLinksAsync(HttpMethod.Put, linkColumnKey, rowIdMap, ct);

    public Task RemoveLinksAsync(string linkColumnKey, IReadOnlyDictionary<string, IEnumerable<string>> rowIdMap, CancellationToken ct = default)
        => _core.ChangeTagLinksAsync(HttpMethod.Delete, linkColumnKey, rowIdMap, ct);

    public Task MergeAsync(string targetTagId, IEnumerable<string> mergedTagIds, CancellationToken ct = default) => _core.MergeTagsAsync(targetTagId, mergedTagIds, ct);
}
