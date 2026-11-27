using System.Text.Json;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>Library metadata server (extended properties such as <c>_description</c>, tags). Seafile 12+ Pro.</summary>
public sealed partial class MetadataApi
{
    /// <summary>
    /// Maximum records per update request accepted by the server. Seafile 14 misapplies values when one request carries
    /// <em>different</em> values for several records, so updates are first grouped by identical values, then batched.
    /// </summary>
    public const int MaxBatchSize = 100;

    /// <summary>Records per update request; overridable by tests to exercise multi-request batching.</summary>
    internal int UpdateBatchSize { get; set; } = MaxBatchSize;

    /// <summary>Object ids per extract-file-details request (server limit 50); overridable by tests.</summary>
    internal int ExtractBatchSize { get; set; } = 50;

    private readonly SeafTransport _t;
    internal MetadataApi(SeafTransport t) => _t = t;

    private static string Base(string libraryId) => $"api/v2.1/repos/{Url.Seg(libraryId)}/metadata/";

    public Task<SeafMetadataStatus> GetStatusAsync(string libraryId, CancellationToken ct = default)
        => _t.GetAsync<SeafMetadataStatus>(Base(libraryId), ct);

    /// <summary>Turns the metadata feature on or off. Enabling triggers a background scan of the library.</summary>
    public Task SetEnabledAsync(string libraryId, bool enabled, CancellationToken ct = default)
        => enabled
            ? _t.SendAsync(HttpMethod.Put, Base(libraryId), SeafTransport.Form(("enabled", "true")), ct)
            : _t.SendAsync(HttpMethod.Delete, Base(libraryId), null, ct);

    /// <summary>Gets the metadata record of a file or folder, or null if the server has not indexed it (yet).</summary>
    public async Task<SeafMetadataRecord?> GetRecordAsync(string libraryId, string path, CancellationToken ct = default)
    {
        path = SeafPath.Normalize(path);
        string json;
        try
        {
            json = await _t.SendForStringAsync(HttpMethod.Get, Url.Of(Base(libraryId) + "record/")
                .Q("parent_dir", SeafPath.GetParent(path)).Q("file_name", SeafPath.GetName(path)), null, ct).ConfigureAwait(false);
        }
        catch (SeafException e) when (e.IsNotFound && e.ServerMessage.Contains("Record", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;
        var fields = results[0].EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        return new SeafMetadataRecord(fields);
    }

    /// <summary>Updates column values of one record.</summary>
    public Task UpdateRecordAsync(string libraryId, string recordId, IReadOnlyDictionary<string, object?> values, CancellationToken ct = default)
        => UpdateRecordsAsync(libraryId, [new SeafMetadataUpdate(recordId, values)], ct);

    /// <summary>Updates many records. Records with identical values share requests of up to <see cref="MaxBatchSize"/>.</summary>
    public Task UpdateRecordsAsync(string libraryId, IEnumerable<SeafMetadataUpdate> updates, CancellationToken ct = default)
        => Core(libraryId).UpdateRecordsAsync(updates, UpdateBatchSize, ct);
}
