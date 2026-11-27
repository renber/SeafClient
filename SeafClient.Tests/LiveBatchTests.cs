using System.IO.Compression;

namespace SeafClient.Tests;

/// <summary>
/// Every batch-style API exercised with two batches of five entries.
/// <list type="bullet">
/// <item>Server-side batch endpoints (one request carries the whole list): two calls of five entries each, then the
/// combined result of all ten is verified.</item>
/// <item>Client-side chunking (the SDK splits a list into several requests): the batch size is lowered to five so ten
/// entries produce exactly two requests, which a recording HTTP handler counts.</item>
/// </list>
/// </summary>
[Collection(LiveCollection.Name)]
public class BatchTests(LiveFixture f)
{
    private const int BatchSize = 5;
    private static readonly string[] Ten = LiveKit.Names(10);
    private static readonly string[] FirstFive = Ten[..BatchSize];
    private static readonly string[] SecondFive = Ten[BatchSize..];

    private SeafSession S => f.Session;

    private static async Task<HashSet<string>> FileNames(SeafSession s, string lib, string dir)
        => (await s.Directories.ListAsync(lib, dir, SeafListFilter.FilesOnly)).Select(e => e.Name).ToHashSet();

    private async Task UploadTen(string lib, string dir)
    {
        foreach (var name in Ten) await LiveKit.Upload(S, lib, dir, name, "content of " + name);
    }

    // ---------------- items ----------------

    [LiveFact]
    public async Task Items_SyncCopyMoveDelete_TwoBatchesOfFive()
    {
        var root = "/batch-items";
        await UploadTen(f.LibraryId, root + "/src");
        await S.Directories.EnsureAsync(f.LibraryId, root + "/copy");
        await S.Directories.EnsureAsync(f.LibraryId, root + "/moved");
        try
        {
            await S.Items.CopyAsync(f.LibraryId, root + "/src", FirstFive, f.LibraryId, root + "/copy");
            Assert.Equal(FirstFive.ToHashSet(), await FileNames(S, f.LibraryId, root + "/copy"));
            await S.Items.CopyAsync(f.LibraryId, root + "/src", SecondFive, f.LibraryId, root + "/copy");
            Assert.Equal(Ten.ToHashSet(), await FileNames(S, f.LibraryId, root + "/copy"));
            Assert.Equal(Ten.ToHashSet(), await FileNames(S, f.LibraryId, root + "/src"));
            Assert.Equal("content of f7.txt", await S.Files.ReadAllTextAsync(f.LibraryId, root + "/copy/f7.txt"));

            await S.Items.MoveAsync(f.LibraryId, root + "/copy", FirstFive, f.LibraryId, root + "/moved");
            await S.Items.MoveAsync(f.LibraryId, root + "/copy", SecondFive, f.LibraryId, root + "/moved");
            Assert.Empty(await FileNames(S, f.LibraryId, root + "/copy"));
            Assert.Equal(Ten.ToHashSet(), await FileNames(S, f.LibraryId, root + "/moved"));
            Assert.Equal("content of f2.txt", await S.Files.ReadAllTextAsync(f.LibraryId, root + "/moved/f2.txt"));

            await S.Items.DeleteAsync(f.LibraryId, root + "/moved", FirstFive);
            Assert.Equal(SecondFive.ToHashSet(), await FileNames(S, f.LibraryId, root + "/moved"));
            await S.Items.DeleteAsync(f.LibraryId, root + "/moved", SecondFive);
            Assert.Empty(await FileNames(S, f.LibraryId, root + "/moved"));
        }
        finally
        {
            await S.Directories.DeleteIfExistsAsync(f.LibraryId, root);
        }
    }

    [LiveFact]
    public async Task Items_AsyncCopyMove_AcrossLibraries_TwoBatchesOfFive()
    {
        var root = "/batch-async";
        await UploadTen(f.LibraryId, root);
        var other = await LiveKit.TempLibrary(S, "SeafClientAsync");
        try
        {
            // Batch 1: poll progress by hand.
            var first = await S.Items.StartCopyAsync(f.LibraryId, root, FirstFive, other.Id, "/");
            Assert.False(first.IsCompleted, "cross-library copies run as server tasks");
            SeafTaskProgress progress;
            do
            {
                progress = await S.Items.GetProgressAsync(first.TaskId);
                if (!progress.IsFinished) await Task.Delay(200);
            } while (!progress.IsFinished);
            Assert.True(progress.Successful);

            // Batch 2: let the SDK wait.
            var second = await S.Items.StartCopyAsync(f.LibraryId, root, SecondFive, other.Id, "/");
            Assert.True((await S.Items.WaitAsync(second)).Successful);
            Assert.Equal(Ten.ToHashSet(), await FileNames(S, other.Id, "/"));

            // Move them back in two batches.
            await S.Directories.EnsureAsync(f.LibraryId, root + "/back");
            await S.Items.WaitAsync(await S.Items.StartMoveAsync(other.Id, "/", FirstFive, f.LibraryId, root + "/back"));
            await S.Items.WaitAsync(await S.Items.StartMoveAsync(other.Id, "/", SecondFive, f.LibraryId, root + "/back"));
            Assert.Empty(await FileNames(S, other.Id, "/"));
            Assert.Equal(Ten.ToHashSet(), await FileNames(S, f.LibraryId, root + "/back"));
            Assert.Equal("content of f9.txt", await S.Files.ReadAllTextAsync(f.LibraryId, root + "/back/f9.txt"));
        }
        finally
        {
            await S.Libraries.DeleteAsync(other.Id);
            await S.Directories.DeleteIfExistsAsync(f.LibraryId, root);
        }
    }

    [LiveFact]
    public async Task Items_ZipOfTenEntries()
    {
        var root = "/batch-zip";
        await UploadTen(f.LibraryId, root);
        try
        {
            await using var zip = await S.Items.OpenZipAsync(f.LibraryId, root, Ten);
            using var buffer = new MemoryStream();
            await zip.CopyToAsync(buffer);
            buffer.Position = 0;
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
            Assert.Equal(Ten.ToHashSet(), archive.Entries.Select(e => e.Name).ToHashSet());
            using var reader = new StreamReader(archive.Entries.Single(e => e.Name == "f6.txt").Open());
            Assert.Equal("content of f6.txt", await reader.ReadToEndAsync());
        }
        finally
        {
            await S.Directories.DeleteIfExistsAsync(f.LibraryId, root);
        }
    }

    [LiveFact]
    public async Task RepoToken_Items_TwoBatchesOfFive()
    {
        var lib = await LiveKit.TempLibrary(S, "SeafClientRTbatch");
        try
        {
            await UploadTen(lib.Id, "/src");
            var token = await S.Libraries.CreateApiTokenAsync(lib.Id, "seafclient-batch", SeafRepoTokenPermission.Rw);
            using var repo = SeafRepoTokenSession.FromToken(LiveKit.ServerUri, token.ApiToken);
            await repo.Directories.EnsureAsync("/copy");
            await repo.Directories.EnsureAsync("/moved");

            await repo.Items.CopyAsync("/src", FirstFive, "/copy");
            await repo.Items.CopyAsync("/src", SecondFive, "/copy");
            Assert.Equal(Ten.ToHashSet(), await FileNames(S, lib.Id, "/copy"));

            await repo.Items.MoveAsync("/copy", FirstFive, "/moved");
            await repo.Items.MoveAsync("/copy", SecondFive, "/moved");
            Assert.Empty(await FileNames(S, lib.Id, "/copy"));
            Assert.Equal(Ten.ToHashSet(), await FileNames(S, lib.Id, "/moved"));

            await repo.Items.DeleteAsync("/moved", FirstFive);
            await repo.Items.DeleteAsync("/moved", SecondFive);
            Assert.Empty(await FileNames(S, lib.Id, "/moved"));
        }
        finally
        {
            foreach (var t in await S.Libraries.ListApiTokensAsync(lib.Id)) await S.Libraries.DeleteApiTokenAsync(lib.Id, t.AppName);
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    // ---------------- metadata (client-side chunking) ----------------

    [LiveFact]
    public async Task Metadata_UpdateRecords_SplitsIntoTwoRequestsOfFive()
    {
        using var s = await SeafSession.LoginAsync(LiveKit.ServerUri, LiveServer.User!, LiveServer.Password!, LiveKit.Recording(out var http));
        var lib = await LiveKit.TempLibrary(s, "SeafClientMetaBatch");
        try
        {
            await s.Metadata.SetEnabledAsync(lib.Id, true);
            foreach (var name in Ten) await LiveKit.Upload(s, lib.Id, "/", name, name);
            var records = await LiveKit.WaitForRecords(s, lib.Id, 10);
            Assert.Equal(10, records.Count);
            const string recordsPath = "/metadata/records/";

            // Same value for all ten records, batch size 5 -> exactly two requests.
            s.Metadata.UpdateBatchSize = BatchSize;
            http.Clear();
            await s.Metadata.UpdateRecordsAsync(lib.Id, records.Select(r => new SeafMetadataUpdate(r.Id, Desc("same"))));
            Assert.Equal(2, http.Count(HttpMethod.Put, recordsPath));
            foreach (var r in records) Assert.Equal("same", (await s.Metadata.GetRecordByIdAsync(lib.Id, r.Id))!.Description);

            // Different value per record: grouped per value (Seafile 14 mixes values within one request), each correct.
            http.Clear();
            await s.Metadata.UpdateRecordsAsync(lib.Id, records.Select(r => new SeafMetadataUpdate(r.Id, Desc("own-" + r.Name))));
            Assert.Equal(10, http.Count(HttpMethod.Put, recordsPath));
            foreach (var r in records) Assert.Equal("own-" + r.Name, (await s.Metadata.GetRecordByIdAsync(lib.Id, r.Id))!.Description);

            // Two value groups of five records each, batch size 5 -> two requests.
            http.Clear();
            await s.Metadata.UpdateRecordsAsync(lib.Id, records.Select((r, i) => new SeafMetadataUpdate(r.Id, Desc(i < BatchSize ? "group-a" : "group-b"))));
            Assert.Equal(2, http.Count(HttpMethod.Put, recordsPath));
            for (var i = 0; i < records.Count; i++)
                Assert.Equal(i < BatchSize ? "group-a" : "group-b", (await s.Metadata.GetRecordByIdAsync(lib.Id, records[i].Id))!.Description);

            // Default batch size (100): one request for all ten identical updates.
            s.Metadata.UpdateBatchSize = SeafClient.Api.MetadataApi.MaxBatchSize;
            http.Clear();
            await s.Metadata.UpdateRecordsAsync(lib.Id, records.Select(r => new SeafMetadataUpdate(r.Id, Desc("default"))));
            Assert.Equal(1, http.Count(HttpMethod.Put, recordsPath));
            foreach (var r in records) Assert.Equal("default", (await s.Metadata.GetRecordByIdAsync(lib.Id, r.Id))!.Description);
        }
        finally
        {
            await s.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task Metadata_ExtractFileDetails_SplitsIntoTwoRequestsOfFive()
    {
        using var s = await SeafSession.LoginAsync(LiveKit.ServerUri, LiveServer.User!, LiveServer.Password!, LiveKit.Recording(out var http));
        var lib = await LiveKit.TempLibrary(s, "SeafClientExtract");
        try
        {
            await s.Metadata.SetEnabledAsync(lib.Id, true);
            foreach (var name in Ten) await LiveKit.Upload(s, lib.Id, "/", name, name);
            var records = await LiveKit.WaitForRecords(s, lib.Id, 10);
            var objIds = records.Select(r => r.GetString("_obj_id")).OfType<string>().ToList();
            Assert.Equal(10, objIds.Count);

            s.Metadata.ExtractBatchSize = BatchSize;
            http.Clear();
            var details = await s.Metadata.ExtractFileDetailsAsync(lib.Id, objIds);
            Assert.Equal(2, http.Count(HttpMethod.Post, "/metadata/extract-file-details/"));
            // Plain text files have no extractable details; the server reports only files it could process.
            Assert.True(details.Count <= 10);
        }
        finally
        {
            await s.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task RepoToken_Metadata_UpdateRecords_SplitsIntoTwoRequestsOfFive()
    {
        var lib = await LiveKit.TempLibrary(S, "SeafClientRTmeta");
        try
        {
            await S.Metadata.SetEnabledAsync(lib.Id, true);
            foreach (var name in Ten) await LiveKit.Upload(S, lib.Id, "/", name, name);
            var records = await LiveKit.WaitForRecords(S, lib.Id, 10);
            Assert.Equal(10, records.Count);

            var token = await S.Libraries.CreateApiTokenAsync(lib.Id, "seafclient-meta", SeafRepoTokenPermission.Rw);
            using var repo = SeafRepoTokenSession.FromToken(LiveKit.ServerUri, token.ApiToken, LiveKit.Recording(out var http));
            repo.Metadata.UpdateBatchSize = BatchSize;
            await repo.Metadata.UpdateRecordsAsync(records.Select(r => new SeafMetadataUpdate(r.Id, Desc("via-token"))));
            Assert.Equal(2, http.Count(HttpMethod.Put, "/via-repo-token/metadata/records/"));
            foreach (var r in records) Assert.Equal("via-token", (await S.Metadata.GetRecordByIdAsync(lib.Id, r.Id))!.Description);
        }
        finally
        {
            foreach (var t in await S.Libraries.ListApiTokensAsync(lib.Id)) await S.Libraries.DeleteApiTokenAsync(lib.Id, t.AppName);
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task MetadataTags_TwoBatchesOfFive()
    {
        var lib = await LiveKit.TempLibrary(S, "SeafClientTagBatch");
        try
        {
            await S.Metadata.SetEnabledAsync(lib.Id, true);
            await S.Metadata.Tags.SetEnabledAsync(lib.Id, true);
            foreach (var name in Ten) await LiveKit.Upload(S, lib.Id, "/", name, name);
            var records = await LiveKit.WaitForRecords(S, lib.Id, 10);
            Assert.Equal(10, records.Count);

            var batch1 = await S.Metadata.Tags.CreateAsync(lib.Id, Enumerable.Range(0, 5).Select(i => new SeafNewMetadataTag($"T{i}")));
            var batch2 = await S.Metadata.Tags.CreateAsync(lib.Id, Enumerable.Range(5, 5).Select(i => new SeafNewMetadataTag($"T{i}")));
            Assert.Equal(5, batch1.Count);
            Assert.Equal(5, batch2.Count);
            var tags = batch1.Concat(batch2).ToList();
            Assert.Equal(10, tags.Select(t => t.Id).Distinct().Count());
            Assert.Equal(10, (await S.Metadata.Tags.ListAsync(lib.Id)).Count);

            // Record i gets tag i; two calls of five records each.
            Dictionary<string, IEnumerable<string>> Map(int from) =>
                Enumerable.Range(from, 5).ToDictionary(i => records[i].Id, i => (IEnumerable<string>)[tags[i].Id]);
            await S.Metadata.Tags.SetFileTagsAsync(lib.Id, Map(0));
            await S.Metadata.Tags.SetFileTagsAsync(lib.Id, Map(5));
            for (var i = 0; i < 10; i++)
            {
                var tagged = (await S.Metadata.Tags.ListFilesAsync(lib.Id, tags[i].Id)).Records;
                Assert.Equal(records[i].Id, Assert.Single(tagged).Id);
            }

            await S.Metadata.Tags.DeleteAsync(lib.Id, batch1.Select(t => t.Id));
            Assert.Equal(5, (await S.Metadata.Tags.ListAsync(lib.Id)).Count);
            await S.Metadata.Tags.DeleteAsync(lib.Id, batch2.Select(t => t.Id));
            Assert.Empty(await S.Metadata.Tags.ListAsync(lib.Id));
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task LegacyFileTags_CreateLibraryTags_TwoBatchesOfFive()
    {
        var lib = await LiveKit.TempLibrary(S, "SeafClientRepoTags");
        try
        {
            var a = await S.FileTags.CreateLibraryTagsAsync(lib.Id, Enumerable.Range(0, 5).Select(i => ($"tag{i}", "#FF8000")));
            var b = await S.FileTags.CreateLibraryTagsAsync(lib.Id, Enumerable.Range(5, 5).Select(i => ($"tag{i}", "#0080FF")));
            Assert.Equal(5, a.Count);
            Assert.Equal(5, b.Count);
            var all = await S.FileTags.ListLibraryTagsAsync(lib.Id);
            Assert.Equal(Enumerable.Range(0, 10).Select(i => $"tag{i}").ToHashSet(), all.Select(t => t.Name).ToHashSet());
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    // ---------------- sharing ----------------

    [LiveFact]
    public async Task Libraries_BatchShare_TwoBatchesOfFiveLibraries()
    {
        var other = (await LiveKit.OtherUsersAsync(S)).First();
        var libs = new List<SeafLibrary>();
        var group = await S.Groups.CreateAsync(LiveKit.Unique("SeafClientBatchGrp", 24));
        try
        {
            for (var i = 0; i < 10; i++) libs.Add(await LiveKit.TempLibrary(S, "SeafClientBatchLib"));
            var ids = libs.Select(l => l.Id).ToArray();

            var g1 = await S.Libraries.BatchShareWithGroupAsync(ids[..5], group.Id, SeafSharePermission.R);
            var g2 = await S.Libraries.BatchShareWithGroupAsync(ids[5..], group.Id, SeafSharePermission.R);
            Assert.Equal(5, g1.Success.Count);
            Assert.Equal(5, g2.Success.Count);
            Assert.Empty(g1.Failed.Concat(g2.Failed));
            Assert.Equal(ids.ToHashSet(), (await S.Groups.ListLibrariesAsync(group.Id)).Select(l => l.Id).ToHashSet());

            var u1 = await S.Libraries.BatchShareWithUserAsync(ids[..5], other);
            var u2 = await S.Libraries.BatchShareWithUserAsync(ids[5..], other);
            Assert.Equal(10, u1.Success.Count + u2.Success.Count);
            foreach (var id in ids) Assert.Contains(await S.Libraries.ListSharesAsync(id), x => x.ShareType == "user" && x.UserInfo?.Name == other);

            Assert.Equal(5, (await S.Libraries.BatchUnshareFromGroupAsync(ids[..5], group.Id)).Success.Count);
            Assert.Equal(5, (await S.Libraries.BatchUnshareFromGroupAsync(ids[5..], group.Id)).Success.Count);
            Assert.Empty(await S.Groups.ListLibrariesAsync(group.Id));

            Assert.Equal(5, (await S.Libraries.BatchUnshareFromUserAsync(ids[..5], other)).Success.Count);
            Assert.Equal(5, (await S.Libraries.BatchUnshareFromUserAsync(ids[5..], other)).Success.Count);
            foreach (var id in ids) Assert.Empty(await S.Libraries.ListSharesAsync(id));
        }
        finally
        {
            foreach (var l in libs) await S.Libraries.DeleteAsync(l.Id);
            await S.Groups.DeleteAsync(group.Id);
        }
    }

    [LiveFact]
    public async Task GroupShares_FolderShares_FolderPermissions_AdminShares_TwoBatchesOfFiveGroups()
    {
        var lib = await LiveKit.TempLibrary(S, "SeafClientGrpShare");
        var groups = new List<SeafGroup>();
        try
        {
            for (var i = 0; i < 10; i++) groups.Add(await S.Groups.CreateAsync(LiveKit.Unique("SeafClientG" + i, 24)));
            var ids = groups.Select(g => g.Id).ToArray();
            await S.Directories.EnsureAsync(lib.Id, "/shared");
            await S.Directories.EnsureAsync(lib.Id, "/perm");
            await S.Directories.EnsureAsync(lib.Id, "/admin");

            // Folder shares with many groups.
            var s1 = await S.Libraries.ShareWithGroupsAsync(lib.Id, ids[..5], SeafSharePermission.R, "/shared");
            var s2 = await S.Libraries.ShareWithGroupsAsync(lib.Id, ids[5..], SeafSharePermission.R, "/shared");
            Assert.Equal(5, s1.Success.Count);
            Assert.Equal(5, s2.Success.Count);
            var shared = (await S.Libraries.ListSharesAsync(lib.Id, "/shared")).Where(x => x.ShareType == "group").Select(x => x.GroupInfo!.Id);
            Assert.Equal(ids.ToHashSet(), shared.ToHashSet());

            // Sub-folder permissions for many groups.
            var p1 = await S.FolderPermissions.AddGroupPermissionsAsync(lib.Id, "/perm", ids[..5], SeafSharePermission.R);
            var p2 = await S.FolderPermissions.AddGroupPermissionsAsync(lib.Id, "/perm", ids[5..], SeafSharePermission.R);
            Assert.Equal(5, p1.Success.Count);
            Assert.Equal(5, p2.Success.Count);
            Assert.Equal(10, (await S.FolderPermissions.ListGroupPermissionsAsync(lib.Id, "/perm")).Count);

            // Admin shares with many groups.
            var a1 = await S.Admin.Shares.ShareWithGroupsAsync(lib.Id, ids[..5], SeafSharePermission.Rw, "/admin");
            var a2 = await S.Admin.Shares.ShareWithGroupsAsync(lib.Id, ids[5..], SeafSharePermission.Rw, "/admin");
            Assert.Equal(5, a1.Success.Count);
            Assert.Equal(5, a2.Success.Count);
            Assert.Equal(10, (await S.Admin.Shares.ListAsync(lib.Id, "/admin")).Count(x => x.ShareType == "group"));
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
            foreach (var g in groups) await S.Groups.DeleteAsync(g.Id);
        }
    }

    [LiveFact]
    public async Task UserBatches_ReportPerEntryResults_TwoBatchesOfFive()
    {
        // The server has too few users for ten real ones, so each batch of five mixes real users and unknown emails
        // and the per-entry success/failure split is verified.
        var others = (await LiveKit.OtherUsersAsync(S)).Take(2).ToList();
        Assert.NotEmpty(others);
        string[] Batch(int n) => [.. others, .. Enumerable.Range(0, 5 - others.Count).Select(i => $"nobody-{n}-{i}-seafclient@example.invalid")];
        var b1 = Batch(1);
        var b2 = Batch(2);

        var lib = await LiveKit.TempLibrary(S, "SeafClientUserBatch");
        var group = await S.Groups.CreateAsync(LiveKit.Unique("SeafClientUB", 24));
        var adminGroup = await S.Admin.Groups.CreateAsync(LiveKit.Unique("SeafClientAUB", 24));
        try
        {
            await S.Directories.EnsureAsync(lib.Id, "/shared");
            await S.Directories.EnsureAsync(lib.Id, "/perm");

            var share1 = await S.Libraries.ShareWithUsersAsync(lib.Id, b1, SeafSharePermission.R, "/shared");
            Assert.Equal(others.Count, share1.Success.Count);
            Assert.Equal(5 - others.Count, share1.Failed.Count);
            var share2 = await S.Libraries.ShareWithUsersAsync(lib.Id, b2, SeafSharePermission.R, "/shared");
            Assert.Equal(5, share2.Success.Count + share2.Failed.Count);
            Assert.Empty(share2.Success); // already shared with the real users

            var perm1 = await S.FolderPermissions.AddUserPermissionsAsync(lib.Id, "/perm", b1, SeafSharePermission.R);
            Assert.Equal(others.Count, perm1.Success.Count);
            Assert.Equal(5 - others.Count, perm1.Failed.Count);
            var perm2 = await S.FolderPermissions.AddUserPermissionsAsync(lib.Id, "/perm", b2, SeafSharePermission.R);
            Assert.Equal(5, perm2.Success.Count + perm2.Failed.Count);

            var m1 = await S.Groups.AddMembersAsync(group.Id, b1);
            Assert.Equal(others.Count, m1.Success.Count);
            Assert.Equal(5 - others.Count, m1.Failed.Count);
            var m2 = await S.Groups.AddMembersAsync(group.Id, b2);
            Assert.Equal(5, m2.Success.Count + m2.Failed.Count);
            Assert.Empty(m2.Success); // already members
            Assert.Equal(others.Count + 1, (await S.Groups.ListMembersAsync(group.Id)).Count);

            var am1 = await S.Admin.Groups.AddMembersAsync(adminGroup.Id, b1);
            Assert.Equal(others.Count, am1.Success.Count);
            Assert.Equal(5 - others.Count, am1.Failed.Count);
            var am2 = await S.Admin.Groups.AddMembersAsync(adminGroup.Id, b2);
            Assert.Equal(5, am2.Success.Count + am2.Failed.Count);
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
            await S.Groups.DeleteAsync(group.Id);
            await S.Admin.Groups.DeleteAsync(adminGroup.Id);
        }
    }

    [LiveFact]
    public async Task ShareLinks_CreateBatch_DeleteMany_TwoBatchesOfFive()
    {
        var dir = "/batch-links";
        await LiveKit.Upload(S, f.LibraryId, dir, "shared.txt", "x");
        try
        {
            var first = await S.ShareLinks.CreateBatchAsync(f.LibraryId, dir + "/shared.txt", 5, autoGeneratePassword: false);
            var second = await S.ShareLinks.CreateBatchAsync(f.LibraryId, dir + "/shared.txt", 5, autoGeneratePassword: true);
            Assert.Equal(5, first.Count);
            Assert.Equal(5, second.Count);
            Assert.All(second, l => Assert.True(l.HasPassword));
            var tokens = first.Concat(second).Select(l => l.Token).ToList();
            Assert.Equal(10, tokens.Distinct().Count());
            Assert.Superset(tokens.ToHashSet(), (await S.ShareLinks.ListAsync(f.LibraryId, dir + "/shared.txt")).Select(l => l.Token).ToHashSet());

            await S.ShareLinks.DeleteManyAsync(tokens[..5]);
            Assert.Equal(tokens[5..].ToHashSet(), (await S.ShareLinks.ListAsync(f.LibraryId, dir + "/shared.txt")).Select(l => l.Token).ToHashSet());
            await S.ShareLinks.DeleteManyAsync(tokens[5..]);
            Assert.Empty(await S.ShareLinks.ListAsync(f.LibraryId, dir + "/shared.txt"));
        }
        finally
        {
            await S.Directories.DeleteIfExistsAsync(f.LibraryId, dir);
        }
    }

    [LiveFact]
    public async Task UploadLinks_DeleteMany_TwoBatchesOfFive()
    {
        var root = "/batch-upload-links";
        var tokens = new List<string>();
        try
        {
            // One upload link per folder is the server's rule, so use ten folders.
            for (var i = 0; i < 10; i++)
            {
                await S.Directories.EnsureAsync(f.LibraryId, $"{root}/d{i}");
                tokens.Add((await S.UploadLinks.CreateAsync(f.LibraryId, $"{root}/d{i}")).Token);
            }
            Assert.Equal(10, tokens.Distinct().Count());

            await S.UploadLinks.DeleteManyAsync(tokens[..5]);
            var remaining = (await S.UploadLinks.ListAsync(f.LibraryId)).Select(l => l.Token).ToHashSet();
            Assert.Empty(remaining.Intersect(tokens[..5]));
            Assert.Superset(tokens[5..].ToHashSet(), remaining);

            await S.UploadLinks.DeleteManyAsync(tokens[5..]);
            Assert.Empty((await S.UploadLinks.ListAsync(f.LibraryId)).Select(l => l.Token).Intersect(tokens));
        }
        finally
        {
            await S.Directories.DeleteIfExistsAsync(f.LibraryId, root);
        }
    }

    private static Dictionary<string, object?> Desc(string value) => new() { ["_description"] = value };
}
