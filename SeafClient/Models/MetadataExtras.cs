using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeafClient.Models;

/// <summary>A column of the metadata table. Record values are keyed by the column <see cref="Name"/>, not its key.</summary>
public sealed record SeafMetadataColumn
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Column type, e.g. <c>text</c>, <c>number</c>, <c>date</c>, <c>checkbox</c>, <c>single-select</c>, <c>link</c>.</summary>
    public string Type { get; init; } = "";

    /// <summary>Type-specific settings (select options, link ids, formats...), or null.</summary>
    public JsonElement? Data { get; init; }
}

/// <summary>
/// The result of a metadata query (records of a view, files of a tag, photos of a person...):
/// the columns that were selected plus the matching rows.
/// </summary>
public sealed record SeafMetadataQueryResult
{
    public IReadOnlyList<SeafMetadataColumn> Columns { get; init; } = [];
    public IReadOnlyList<SeafMetadataRecord> Records { get; init; } = [];
}

/// <summary>A saved view of the metadata table (Seafile 12+ Pro).</summary>
public sealed record SeafMetadataView
{
    [JsonPropertyName("_id")] public string Id { get; init; } = "";
    public string TableId { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>View type: <c>table</c>, <c>gallery</c>, <c>kanban</c>, <c>map</c>, <c>statistics</c>, <c>face_recognition</c>...</summary>
    public string Type { get; init; } = "table";

    /// <summary><c>And</c> or <c>Or</c>.</summary>
    public string? FilterConjunction { get; init; }

    public IReadOnlyList<JsonElement> Filters { get; init; } = [];
    public IReadOnlyList<JsonElement> BasicFilters { get; init; } = [];
    public IReadOnlyList<SeafMetadataSort> Sorts { get; init; } = [];
    public IReadOnlyList<JsonElement> Groupbys { get; init; } = [];
    public IReadOnlyList<string> HiddenColumns { get; init; } = [];
}

/// <summary>A sort of a metadata view.</summary>
public sealed record SeafMetadataSort
{
    public string ColumnKey { get; init; } = "";

    /// <summary><c>up</c> (ascending) or <c>down</c> (descending).</summary>
    public string SortType { get; init; } = "up";
}

/// <summary>All views of a library plus their order/grouping in the sidebar.</summary>
public sealed record SeafMetadataViewList
{
    public IReadOnlyList<SeafMetadataView> Views { get; init; } = [];
    public IReadOnlyList<SeafMetadataNavigationItem> Navigation { get; init; } = [];
}

/// <summary>A view or folder in the view sidebar. Folders contain views in <see cref="Children"/>.</summary>
public sealed record SeafMetadataNavigationItem
{
    [JsonPropertyName("_id")] public string Id { get; init; } = "";

    /// <summary><c>view</c> or <c>folder</c>.</summary>
    public string Type { get; init; } = "view";

    /// <summary>Folder name (views are named in <see cref="SeafMetadataView.Name"/>).</summary>
    public string? Name { get; init; }

    public IReadOnlyList<SeafMetadataNavigationItem>? Children { get; init; }
}

/// <summary>A link from one metadata row to another (e.g. a tag on a file).</summary>
public sealed record SeafMetadataLink
{
    public string RowId { get; init; } = "";
    public string? DisplayValue { get; init; }
}

/// <summary>A tag of the library tag system (Seafile 13+, <c>/metadata/tags/</c>).</summary>
public sealed record SeafMetadataTag
{
    [JsonPropertyName("_id")] public string Id { get; init; } = "";
    [JsonPropertyName("_tag_name")] public string Name { get; init; } = "";

    /// <summary>Hex color such as <c>#F4667C</c>, or null.</summary>
    [JsonPropertyName("_tag_color")] public string? Color { get; init; }

    [JsonPropertyName("_creator")] public string? Creator { get; init; }
    [JsonPropertyName("_last_modifier")] public string? LastModifier { get; init; }
    [JsonPropertyName("_ctime")] public DateTimeOffset? Created { get; init; }
    [JsonPropertyName("_mtime")] public DateTimeOffset? Modified { get; init; }

    /// <summary>Metadata records (files) carrying this tag.</summary>
    [JsonPropertyName("_tag_file_links")] public IReadOnlyList<SeafMetadataLink> FileLinks { get; init; } = [];

    [JsonPropertyName("_tag_parent_links")] public IReadOnlyList<SeafMetadataLink> ParentLinks { get; init; } = [];
    [JsonPropertyName("_tag_sub_links")] public IReadOnlyList<SeafMetadataLink> SubLinks { get; init; } = [];
}

/// <summary>A tag to create.</summary>
public sealed record SeafNewMetadataTag(string Name, string? Color = null);

/// <summary>A change to an existing tag; null fields are left untouched.</summary>
public sealed record SeafMetadataTagUpdate(string TagId, string? Name = null, string? Color = null);

/// <summary>Link column keys of the tags table, for the tag link endpoints.</summary>
public static class SeafTagLinkColumns
{
    /// <summary>Tag → files (record ids).</summary>
    public const string Files = "_tag_file_links";

    /// <summary>Tag → parent tags.</summary>
    public const string Parents = "_tag_parent_links";

    /// <summary>Tag → child tags.</summary>
    public const string Children = "_tag_sub_links";
}

/// <summary>Per-record result of setting file tags through a repo API token.</summary>
public sealed record SeafFileTagsResult
{
    public IReadOnlyList<string> Success { get; init; } = [];
    public IReadOnlyList<string> Fail { get; init; } = [];
}

/// <summary>Details extracted from a file (EXIF and similar) by <c>extract-file-details</c>.</summary>
public sealed record SeafExtractedFileDetails
{
    [JsonPropertyName("_id")] public string RecordId { get; init; } = "";

    /// <summary>Server-formatted details (a Markdown/JSON block).</summary>
    [JsonPropertyName("_file_details")] public string? FileDetails { get; init; }

    /// <summary>GPS location, e.g. <c>{"lat": 39.08, "lng": 115.2}</c>, or null.</summary>
    [JsonPropertyName("_location")] public JsonElement? Location { get; init; }
}
