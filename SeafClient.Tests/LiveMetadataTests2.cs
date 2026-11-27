using System.Net;
using System.Text;

namespace SeafClient.Tests;

/// <summary>Records, columns, views and tags. Uses its own library so metadata can be toggled freely.</summary>
[Collection(LiveCollection.Name)]
public class MetadataExtrasTests(LiveFixture f) : IAsyncLifetime
{
    private SeafLibrary _lib = null!;
    private string Lib => _lib.Id;
    private SeafSession S => f.Session;

    public async Task InitializeAsync()
    {
        if (!LiveServer.IsConfigured) return;
        _lib = await f.Session.Libraries.CreateAsync($"SeafClientMeta-{Guid.NewGuid():N}"[..27]);
    }

    public async Task DisposeAsync()
    {
        if (_lib != null) await f.Session.Libraries.DeleteAsync(_lib.Id);
    }

    private async Task<bool> EnableAsync()
    {
        if (!(await S.Server.GetInfoAsync()).IsPro) return false;
        await S.Metadata.SetEnabledAsync(Lib, true);
        return true;
    }

    private async Task Upload(string dir, string name, string text)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await S.Files.UploadAsync(Lib, dir, name, ms, new SeafUploadOptions { CreateParents = true });
    }

    /// <summary>Indexing is asynchronous: poll a view until <paramref name="count"/> records show up.</summary>
    private async Task<SeafMetadataQueryResult> WaitForRecords(string viewId, int count)
    {
        SeafMetadataQueryResult result = new();
        for (var i = 0; i < 40; i++)
        {
            result = await S.Metadata.ListRecordsAsync(Lib, viewId);
            if (result.Records.Count >= count) return result;
            await Task.Delay(500);
        }
        return result;
    }

    [LiveFact]
    public async Task Records_Columns_Views()
    {
        if (!await EnableAsync()) return;
        await Upload("/", "a.txt", "alpha");
        await Upload("/", "b.txt", "bravo");

        var views = await S.Metadata.Views.ListAsync(Lib);
        var all = Assert.Single(views.Views);
        Assert.Equal("All files", all.Name);
        Assert.Contains(views.Navigation, n => n.Id == all.Id && n.Type == "view");

        var records = await WaitForRecords(all.Id, 2);
        Assert.Equal(2, records.Records.Count);
        Assert.Contains(records.Columns, c => c.Name == "_description" && c.Type == "long-text");
        var a = records.Records.Single(r => r.Name == "a.txt");
        var b = records.Records.Single(r => r.Name == "b.txt");
        Assert.Equal("/", a.ParentDir);

        // Paging.
        Assert.Single((await S.Metadata.ListRecordsAsync(Lib, all.Id, start: 1, limit: 1)).Records);

        // Batch update with different values per record (regression: Seafile 14 mixes them up in one request).
        await S.Metadata.UpdateRecordsAsync(Lib,
        [
            new SeafMetadataUpdate(a.Id, new Dictionary<string, object?> { ["_description"] = "desc-a" }),
            new SeafMetadataUpdate(b.Id, new Dictionary<string, object?> { ["_description"] = "desc-b" }),
        ]);
        Assert.Equal("desc-a", (await S.Metadata.GetRecordByIdAsync(Lib, a.Id))!.Description);
        Assert.Equal("desc-b", (await S.Metadata.GetRecordAsync(Lib, "/b.txt"))!.Description);

        // Single update by path, and lookups of unknown records.
        await S.Metadata.UpdateRecordByPathAsync(Lib, "/a.txt", new Dictionary<string, object?> { ["_description"] = "by-path" });
        Assert.Equal("by-path", (await S.Metadata.GetRecordByIdAsync(Lib, a.Id))!.Description);
        Assert.Null(await S.Metadata.GetRecordByIdAsync(Lib, "doesNotExist0000000000"));

        // Custom columns: values are keyed by column name.
        var column = await S.Metadata.AddColumnAsync(Lib, "Client", "text");
        Assert.Equal("Client", column.Name);
        Assert.False(string.IsNullOrEmpty(column.Key));
        var dup = await Assert.ThrowsAsync<SeafException>(() => S.Metadata.AddColumnAsync(Lib, "Client"));
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        await S.Metadata.UpdateRecordAsync(Lib, a.Id, new Dictionary<string, object?> { ["Client"] = "Acme" });
        Assert.Equal("Acme", (await S.Metadata.GetRecordByIdAsync(Lib, a.Id))!.GetString("Client"));
        var renamed = await S.Metadata.UpdateColumnAsync(Lib, column.Key, newName: "Customer");
        Assert.Equal("Customer", renamed.Name);
        await S.Metadata.DeleteColumnAsync(Lib, column.Key);

        // Details settings are merged into the status.
        await S.Metadata.UpdateDetailsSettingsAsync(Lib, new Dictionary<string, object?> { ["seafclient"] = 1 });
        Assert.Contains("seafclient", (await S.Metadata.GetStatusAsync(Lib)).DetailsSettings);

        // Text files have no extractable details; the call succeeds with an empty list.
        var objId = a.GetString("_obj_id")!;
        Assert.Empty(await S.Metadata.ExtractFileDetailsAsync(Lib, [objId]));

        // Views: create, update, get, duplicate, move, delete; folders.
        var view = await S.Metadata.Views.CreateAsync(Lib, "Docs");
        Assert.Equal("table", view.Type);
        await S.Metadata.Views.UpdateAsync(Lib, view.Id, new { name = "Docs2", sorts = new[] { new { column_key = "_name", sort_type = "up" } } });
        var got = await S.Metadata.Views.GetAsync(Lib, view.Id);
        Assert.Equal("Docs2", got.Name);
        Assert.Equal("_name", Assert.Single(got.Sorts).ColumnKey);
        var copy = await S.Metadata.Views.DuplicateAsync(Lib, view.Id);
        Assert.StartsWith("Docs2", copy.Name);
        Assert.NotEqual(view.Id, copy.Id);
        var nav = await S.Metadata.Views.MoveAsync(Lib, copy.Id, all.Id);
        Assert.Equal(copy.Id, nav[0].Id);
        await S.Metadata.Views.RenameAsync(Lib, copy.Id, "Copy");
        Assert.Equal("Copy", (await S.Metadata.Views.GetAsync(Lib, copy.Id)).Name);
        await S.Metadata.Views.DeleteAsync(Lib, copy.Id);
        Assert.DoesNotContain((await S.Metadata.Views.ListAsync(Lib)).Views, v => v.Id == copy.Id);
        await Assert.ThrowsAsync<SeafException>(() => S.Metadata.Views.GetAsync(Lib, "nope"));

        var folder = await S.Metadata.Views.CreateFolderAsync(Lib, "Folder");
        Assert.Equal("folder", folder.Type);
        await S.Metadata.Views.RenameFolderAsync(Lib, folder.Id, "Folder2");
        Assert.Contains((await S.Metadata.Views.ListAsync(Lib)).Navigation, n => n.Id == folder.Id && n.Name == "Folder2");
        var inFolder = await S.Metadata.Views.CreateAsync(Lib, "Inside", folderId: folder.Id);
        Assert.Contains((await S.Metadata.Views.ListAsync(Lib)).Navigation.Single(n => n.Id == folder.Id).Children!, c => c.Id == inFolder.Id);
        await S.Metadata.Views.DeleteAsync(Lib, inFolder.Id, folder.Id);
        await S.Metadata.Views.DeleteFolderAsync(Lib, folder.Id);
        Assert.DoesNotContain((await S.Metadata.Views.ListAsync(Lib)).Navigation, n => n.Id == folder.Id);
    }

    [LiveFact]
    public async Task Tags_Lifecycle()
    {
        if (!await EnableAsync()) return;
        Assert.True((await S.Metadata.GetStatusAsync(Lib)).TagsEnabled);
        await Upload("/", "a.txt", "alpha");
        await Upload("/", "b.txt", "bravo");
        var all = (await S.Metadata.Views.ListAsync(Lib)).Views[0];
        var records = await WaitForRecords(all.Id, 2);
        var a = records.Records.Single(r => r.Name == "a.txt").Id;
        var b = records.Records.Single(r => r.Name == "b.txt").Id;

        var created = await S.Metadata.Tags.CreateAsync(Lib, [new SeafNewMetadataTag("Urgent", "#F4667C"), new SeafNewMetadataTag("Review"), new SeafNewMetadataTag("Dup")]);
        Assert.Equal(3, created.Count);
        var urgent = created.Single(t => t.Name == "Urgent");
        var review = created.Single(t => t.Name == "Review");
        var dup = created.Single(t => t.Name == "Dup");
        Assert.Equal("#F4667C", urgent.Color);
        Assert.NotNull(urgent.Created);

        // Creating an existing name returns the existing tag unchanged.
        var again = await S.Metadata.Tags.CreateAsync(Lib, "Urgent", "#000000");
        Assert.Equal(urgent.Id, again.Id);
        Assert.Equal("#F4667C", again.Color);

        await S.Metadata.Tags.SetFileTagsAsync(Lib, new Dictionary<string, IEnumerable<string>> { [a] = [urgent.Id, review.Id], [b] = [dup.Id] });
        var urgentFiles = await S.Metadata.Tags.ListFilesAsync(Lib, urgent.Id);
        Assert.Equal("a.txt", Assert.Single(urgentFiles.Records).Name);
        Assert.Equal(2, (await S.Metadata.Tags.ListFilesAsync(Lib, [urgent.Id, dup.Id])).Records.Count);
        Assert.Empty((await S.Metadata.Tags.ListFilesAsync(Lib, Array.Empty<string>())).Records);

        await S.Metadata.Tags.UpdateAsync(Lib, [new SeafMetadataTagUpdate(review.Id, Name: "Reviewed", Color: "#123456")]);
        await S.Metadata.Tags.AddLinksAsync(Lib, SeafTagLinkColumns.Children, new Dictionary<string, IEnumerable<string>> { [urgent.Id] = [review.Id] });
        var tags = await S.Metadata.Tags.ListAsync(Lib);
        Assert.Equal("Reviewed", tags.Single(t => t.Id == review.Id).Name);
        Assert.Contains(tags.Single(t => t.Id == urgent.Id).SubLinks, l => l.RowId == review.Id);
        Assert.Contains(tags.Single(t => t.Id == review.Id).ParentLinks, l => l.RowId == urgent.Id);
        Assert.Contains(tags.Single(t => t.Id == urgent.Id).FileLinks, l => l.RowId == a);

        await S.Metadata.Tags.RemoveLinksAsync(Lib, SeafTagLinkColumns.Children, new Dictionary<string, IEnumerable<string>> { [urgent.Id] = [review.Id] });
        await S.Metadata.Tags.SetLinksAsync(Lib, SeafTagLinkColumns.Files, new Dictionary<string, IEnumerable<string>> { [urgent.Id] = [a, b] });
        Assert.Equal(2, (await S.Metadata.Tags.ListFilesAsync(Lib, urgent.Id)).Records.Count);

        await S.Metadata.Tags.MergeAsync(Lib, urgent.Id, [dup.Id]);
        await S.Metadata.Tags.DeleteAsync(Lib, [review.Id]);
        var remaining = Assert.Single(await S.Metadata.Tags.ListAsync(Lib));
        Assert.Equal(urgent.Id, remaining.Id);

        await S.Metadata.Tags.SetFileTagsAsync(Lib, a, []);
        Assert.DoesNotContain((await S.Metadata.Tags.ListFilesAsync(Lib, urgent.Id)).Records, r => r.Id == a);

        await S.Metadata.Tags.SetEnabledAsync(Lib, false);
        Assert.False((await S.Metadata.GetStatusAsync(Lib)).TagsEnabled);
        await S.Metadata.Tags.SetEnabledAsync(Lib, true);
        Assert.True((await S.Metadata.GetStatusAsync(Lib)).TagsEnabled);
        Assert.Empty(await S.Metadata.Tags.ListAsync(Lib));
    }
}
