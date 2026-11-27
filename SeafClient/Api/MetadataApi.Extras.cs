using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

public sealed partial class MetadataApi
{
    /// <summary>Saved views of the metadata table.</summary>
    public MetadataViewsApi Views { get; }

    /// <summary>The library tag system (Seafile 13+).</summary>
    public MetadataTagsApi Tags { get; }

    internal MetadataCore Core(string libraryId) => new(_t, Base(libraryId));

    // ---------------- records ----------------

    /// <summary>Lists the records of a view (see <see cref="MetadataViewsApi.ListAsync"/> for view ids), with the view's filters and sorts applied.</summary>
    public Task<SeafMetadataQueryResult> ListRecordsAsync(string libraryId, string viewId, int start = 0, int limit = 1000, CancellationToken ct = default)
        => Core(libraryId).ListRecordsAsync(viewId, start, limit, ct);

    /// <summary>Gets a record by its id, or null if there is no such record.</summary>
    public async Task<SeafMetadataRecord?> GetRecordByIdAsync(string libraryId, string recordId, CancellationToken ct = default)
    {
        string json;
        try
        {
            json = await _t.SendForStringAsync(HttpMethod.Get, Url.Of(Base(libraryId) + "record/").Q("record_id", recordId), null, ct).ConfigureAwait(false);
        }
        catch (SeafException e) when (e.IsNotFound && e.ServerMessage.Contains("Record", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return MetadataCore.ParseQuery(json).Records.FirstOrDefault();
    }

    /// <summary>
    /// Updates the column values of the record of a file or folder, addressed by path. Values are keyed by column
    /// <em>name</em> (built-in columns start with '_', e.g. <c>_description</c>); unknown and read-only columns are ignored.
    /// </summary>
    /// <exception cref="SeafException">404 if the item has not been indexed (yet).</exception>
    public Task UpdateRecordByPathAsync(string libraryId, string path, IReadOnlyDictionary<string, object?> values, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        return _t.SendAsync(HttpMethod.Put, Base(libraryId) + "record/", SeafTransport.Json(new
        {
            parent_dir = SeafPath.GetParent(path),
            file_name = SeafPath.GetName(path),
            data = values,
        }), ct);
    }

    // ---------------- columns ----------------

    /// <summary>
    /// Adds a custom column. <paramref name="type"/> is a metadata column type such as <c>text</c>, <c>number</c>,
    /// <c>date</c>, <c>checkbox</c>, <c>single-select</c>, <c>long-text</c>, <c>rate</c>, <c>collaborator</c>.
    /// </summary>
    /// <exception cref="SeafException">400 if a column with this name exists.</exception>
    public async Task<SeafMetadataColumn> AddColumnAsync(string libraryId, string name, string type = "text", string? key = null, object? data = null, CancellationToken ct = default)
        => (await _t.SendAsync<ColumnResponse>(HttpMethod.Post, Base(libraryId) + "columns/",
            SeafTransport.Json(new { column_name = name, column_type = type, column_key = key, column_data = data }), ct).ConfigureAwait(false)).Column;

    /// <summary>Renames a custom column and/or merges <paramref name="data"/> into its settings.</summary>
    public async Task<SeafMetadataColumn> UpdateColumnAsync(string libraryId, string key, string? newName = null, object? data = null, CancellationToken ct = default)
    {
        if (newName == null && data == null) throw new ArgumentException("Pass a new name and/or data.");
        return (await _t.SendAsync<ColumnResponse>(HttpMethod.Put, Base(libraryId) + "columns/",
            SeafTransport.Json(new { column_key = key, name = newName, data }), ct).ConfigureAwait(false)).Column;
    }

    public Task DeleteColumnAsync(string libraryId, string key, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, Base(libraryId) + "columns/", SeafTransport.Json(new { column_key = key }), ct);

    // ---------------- settings and features ----------------

    /// <summary>Merges <paramref name="settings"/> into the library's details-panel settings (see <see cref="SeafMetadataStatus"/>).</summary>
    public Task UpdateDetailsSettingsAsync(string libraryId, IReadOnlyDictionary<string, object?> settings, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Put, Base(libraryId) + "details-settings/", SeafTransport.Json(new { settings }), ct);

    /// <summary>
    /// Extracts details (EXIF, capture time, location...) for files by object id (<c>_obj_id</c> of their records).
    /// Requests are split into batches of 50. Files without extractable details are omitted.
    /// </summary>
    public async Task<IReadOnlyList<SeafExtractedFileDetails>> ExtractFileDetailsAsync(string libraryId, IEnumerable<string> objectIds, CancellationToken ct = default)
    {
        var result = new List<SeafExtractedFileDetails>();
        foreach (var chunk in objectIds.Chunk(ExtractBatchSize))
        {
            var r = await _t.SendAsync<DetailsResponse>(HttpMethod.Post, Base(libraryId) + "extract-file-details/",
                SeafTransport.Json(new { obj_ids = chunk }), ct).ConfigureAwait(false);
            result.AddRange(r.Details);
        }
        return result;
    }

    private sealed record ColumnResponse { public SeafMetadataColumn Column { get; init; } = new(); }
    private sealed record DetailsResponse { public List<SeafExtractedFileDetails> Details { get; init; } = []; }
}

/// <summary>Saved views of a library's metadata table (Seafile 12+ Pro).</summary>
public sealed class MetadataViewsApi
{
    private readonly SeafTransport _t;
    internal MetadataViewsApi(SeafTransport t) => _t = t;

    private static string Base(string libraryId) => $"api/v2.1/repos/{Url.Seg(libraryId)}/metadata/";
    private MetadataCore Core(string libraryId) => new(_t, Base(libraryId));

    /// <summary>Lists all views and the sidebar navigation (order and folders). Enabling metadata creates an "All files" view.</summary>
    public Task<SeafMetadataViewList> ListAsync(string libraryId, CancellationToken ct = default) => Core(libraryId).ListViewsAsync(ct);

    public Task<SeafMetadataView> GetAsync(string libraryId, string viewId, CancellationToken ct = default) => Core(libraryId).GetViewAsync(viewId, ct);

    /// <summary>Creates a view, optionally inside a navigation folder. <paramref name="data"/> sets initial view properties (filters, sorts...).</summary>
    public Task<SeafMetadataView> CreateAsync(string libraryId, string name, string type = "table", string? folderId = null, object? data = null, CancellationToken ct = default)
        => Core(libraryId).CreateViewAsync(name, type, folderId, data, ct);

    /// <summary>
    /// Updates view properties. <paramref name="viewData"/> is merged into the view, e.g.
    /// <c>new { name = "Docs", sorts = new[] { new { column_key = "_name", sort_type = "up" } } }</c>.
    /// </summary>
    public Task UpdateAsync(string libraryId, string viewId, object viewData, CancellationToken ct = default)
        => Core(libraryId).UpdateViewAsync(viewId, viewData, ct);

    public Task RenameAsync(string libraryId, string viewId, string newName, CancellationToken ct = default)
        => Core(libraryId).UpdateViewAsync(viewId, new { name = newName }, ct);

    /// <summary>Deletes a view. Pass <paramref name="folderId"/> if the view sits in a navigation folder.</summary>
    public Task DeleteAsync(string libraryId, string viewId, string? folderId = null, CancellationToken ct = default)
        => Core(libraryId).DeleteViewAsync(viewId, folderId, ct);

    /// <summary>Copies a view; the copy is named like <c>Docs (1)</c>.</summary>
    public Task<SeafMetadataView> DuplicateAsync(string libraryId, string viewId, string? folderId = null, CancellationToken ct = default)
        => Core(libraryId).DuplicateViewAsync(viewId, folderId, ct);

    /// <summary>
    /// Moves a view (or folder) above <paramref name="targetViewId"/>, or into <paramref name="targetFolderId"/>.
    /// Returns the new navigation.
    /// </summary>
    public Task<IReadOnlyList<SeafMetadataNavigationItem>> MoveAsync(string libraryId, string? sourceViewId, string? targetViewId, string? targetFolderId = null, string? sourceFolderId = null, bool isAboveFolder = false, CancellationToken ct = default)
        => Core(libraryId).MoveViewAsync(sourceViewId, sourceFolderId, targetViewId, targetFolderId, isAboveFolder, ct);

    // ---- navigation folders ----

    public async Task<SeafMetadataNavigationItem> CreateFolderAsync(string libraryId, string name, CancellationToken ct = default)
        => (await _t.SendAsync<FolderResponse>(HttpMethod.Post, Base(libraryId) + "folders/", SeafTransport.Json(new { name }), ct).ConfigureAwait(false)).Folder;

    public Task RenameFolderAsync(string libraryId, string folderId, string newName, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Put, Base(libraryId) + "folders/", SeafTransport.Json(new { folder_id = folderId, folder_data = new { name = newName } }), ct);

    /// <summary>Deletes a navigation folder (its views move to the top level).</summary>
    public Task DeleteFolderAsync(string libraryId, string folderId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, Base(libraryId) + "folders/", SeafTransport.Json(new { folder_id = folderId }), ct);

    private sealed record FolderResponse { public SeafMetadataNavigationItem Folder { get; init; } = new(); }
}

/// <summary>The library tag system (<c>/metadata/tags/</c>, Seafile 13+). Tags attach to metadata records (files).</summary>
public sealed class MetadataTagsApi
{
    private readonly SeafTransport _t;
    internal MetadataTagsApi(SeafTransport t) => _t = t;

    private MetadataCore Core(string libraryId) => new(_t, $"api/v2.1/repos/{Url.Seg(libraryId)}/metadata/");

    /// <summary>Turns the tag feature on or off. Enabling metadata turns tags on automatically; disabling deletes all tags.</summary>
    public Task SetEnabledAsync(string libraryId, bool enabled, string lang = "en", CancellationToken ct = default)
        => Core(libraryId).SetTagsEnabledAsync(enabled, lang, ct);

    public Task<IReadOnlyList<SeafMetadataTag>> ListAsync(string libraryId, int start = 0, int limit = 1000, CancellationToken ct = default)
        => Core(libraryId).ListTagsAsync(start, limit, ct);

    /// <summary>Creates tags. Names that already exist are not duplicated: the existing tag is returned instead (unchanged).</summary>
    public Task<IReadOnlyList<SeafMetadataTag>> CreateAsync(string libraryId, IEnumerable<SeafNewMetadataTag> tags, CancellationToken ct = default)
        => Core(libraryId).CreateTagsAsync(tags, ct);

    public async Task<SeafMetadataTag> CreateAsync(string libraryId, string name, string? color = null, CancellationToken ct = default)
        => (await CreateAsync(libraryId, [new SeafNewMetadataTag(name, color)], ct).ConfigureAwait(false)).First();

    public Task UpdateAsync(string libraryId, IEnumerable<SeafMetadataTagUpdate> updates, CancellationToken ct = default)
        => Core(libraryId).UpdateTagsAsync(updates, ct);

    public Task DeleteAsync(string libraryId, IEnumerable<string> tagIds, CancellationToken ct = default)
        => Core(libraryId).DeleteTagsAsync(tagIds, ct);

    /// <summary>Replaces the tags of a file (by metadata record id). Pass an empty list to remove all tags.</summary>
    public Task SetFileTagsAsync(string libraryId, string recordId, IEnumerable<string> tagIds, CancellationToken ct = default)
        => SetFileTagsAsync(libraryId, new Dictionary<string, IEnumerable<string>> { [recordId] = tagIds }, ct);

    /// <summary>Replaces the tags of several files at once (record id → tag ids).</summary>
    public Task SetFileTagsAsync(string libraryId, IReadOnlyDictionary<string, IEnumerable<string>> recordTags, CancellationToken ct = default)
        => Core(libraryId).SetFileTagsAsync(recordTags, ct);

    /// <summary>Lists the files carrying a tag (columns <c>_id</c>, <c>_name</c>, <c>_parent_dir</c>, <c>_size</c>, <c>_file_mtime</c>, <c>_tags</c>).</summary>
    public Task<SeafMetadataQueryResult> ListFilesAsync(string libraryId, string tagId, CancellationToken ct = default)
        => Core(libraryId).ListTagFilesAsync(tagId, ct);

    /// <summary>Lists the files carrying any of the tags.</summary>
    public Task<SeafMetadataQueryResult> ListFilesAsync(string libraryId, IEnumerable<string> tagIds, CancellationToken ct = default)
        => Core(libraryId).ListTagsFilesAsync(tagIds, ct);

    /// <summary>
    /// Adds links from tags to rows. <paramref name="linkColumnKey"/> is one of <see cref="SeafTagLinkColumns"/>;
    /// the map is tag id → linked row ids (record ids for <see cref="SeafTagLinkColumns.Files"/>, tag ids otherwise).
    /// </summary>
    public Task AddLinksAsync(string libraryId, string linkColumnKey, IReadOnlyDictionary<string, IEnumerable<string>> rowIdMap, CancellationToken ct = default)
        => Core(libraryId).ChangeTagLinksAsync(HttpMethod.Post, linkColumnKey, rowIdMap, ct);

    /// <summary>Replaces the links of each tag in the map.</summary>
    public Task SetLinksAsync(string libraryId, string linkColumnKey, IReadOnlyDictionary<string, IEnumerable<string>> rowIdMap, CancellationToken ct = default)
        => Core(libraryId).ChangeTagLinksAsync(HttpMethod.Put, linkColumnKey, rowIdMap, ct);

    public Task RemoveLinksAsync(string libraryId, string linkColumnKey, IReadOnlyDictionary<string, IEnumerable<string>> rowIdMap, CancellationToken ct = default)
        => Core(libraryId).ChangeTagLinksAsync(HttpMethod.Delete, linkColumnKey, rowIdMap, ct);

    /// <summary>Merges tags into <paramref name="targetTagId"/> (files and hierarchy move over) and deletes the merged tags.</summary>
    public Task MergeAsync(string libraryId, string targetTagId, IEnumerable<string> mergedTagIds, CancellationToken ct = default)
        => Core(libraryId).MergeTagsAsync(targetTagId, mergedTagIds, ct);
}
