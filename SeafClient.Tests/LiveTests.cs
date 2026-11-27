using System.Net;
using System.Text;

namespace SeafClient.Tests;

[Collection(LiveCollection.Name)]
public class SessionAndLibraryTests(LiveFixture f)
{
    [LiveFact]
    public async Task Login_WithBadPassword_Throws400()
    {
        var ex = await Assert.ThrowsAsync<SeafException>(() => SeafSession.LoginAsync(new Uri(LiveServer.Url!), LiveServer.User!, "definitely-wrong"));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("credentials", ex.ServerMessage, StringComparison.OrdinalIgnoreCase);
    }

    [LiveFact]
    public async Task FromToken_ReusesToken()
    {
        using var s = SeafSession.FromToken(new Uri(LiveServer.Url!), f.Session.AuthToken);
        Assert.True(await s.Server.AuthPingAsync());
        using var bad = SeafSession.FromToken(new Uri(LiveServer.Url!), "0000000000000000000000000000000000000000");
        Assert.False(await bad.Server.AuthPingAsync());
    }

    [LiveFact]
    public async Task ServerAndAccountInfo()
    {
        Assert.True(await f.Session.Server.PingAsync());
        var info = await f.Session.Server.GetInfoAsync();
        Assert.NotNull(info.ParsedVersion);
        var account = await f.Session.Server.GetAccountInfoAsync();
        Assert.Equal(LiveServer.User, account.Email, ignoreCase: true);
        Assert.False(string.IsNullOrEmpty(await f.Session.Server.GetAvatarUrlAsync(64)));
    }

    [LiveFact]
    public async Task Libraries_ListGetFindRename()
    {
        var mine = await f.Session.Libraries.ListAsync(SeafLibraryType.Mine);
        Assert.Contains(mine, l => l.Id == f.LibraryId && l.Type == "mine");

        var all = await f.Session.Libraries.ListDistinctAsync();
        Assert.Equal(all.Count, all.Select(l => l.Id).Distinct().Count());

        var found = await f.Session.Libraries.FindByNameAsync(f.Library.Name);
        Assert.Equal(f.LibraryId, found?.Id);

        var temp = await f.Session.Libraries.CreateAsync($"SeafClientTmp-{Guid.NewGuid():N}"[..24]);
        try
        {
            await f.Session.Libraries.RenameAsync(temp.Id, temp.Name + "-r");
            Assert.Equal(temp.Name + "-r", (await f.Session.Libraries.GetAsync(temp.Id)).Name);
        }
        finally
        {
            await f.Session.Libraries.DeleteAsync(temp.Id);
        }
        await Assert.ThrowsAsync<SeafException>(() => f.Session.Libraries.GetAsync(temp.Id));
    }
}

[Collection(LiveCollection.Name)]
public class DirectoryTests(LiveFixture f)
{
    private string Root([System.Runtime.CompilerServices.CallerMemberName] string name = "") => "/dirs-" + name;

    [LiveFact]
    public async Task Create_Conflict_Ensure()
    {
        var root = Root();
        var created = await f.Session.Directories.CreateAsync(f.LibraryId, root);
        Assert.Equal(root, created.Path);

        var conflict = await Assert.ThrowsAsync<SeafException>(() => f.Session.Directories.CreateAsync(f.LibraryId, root));
        Assert.True(conflict.IsConflict);

        // Ensure is idempotent and never produces "name (1)".
        await f.Session.Directories.EnsureAsync(f.LibraryId, root);
        var deep = await f.Session.Directories.EnsureAsync(f.LibraryId, root + "/a/b/c");
        Assert.Equal(root + "/a/b/c", deep.Path);
        await f.Session.Directories.EnsureAsync(f.LibraryId, root + "/a/b/c");

        var top = await f.Session.Directories.ListAsync(f.LibraryId, root);
        Assert.Single(top);
        Assert.Equal(root + "/a", top[0].Path);
        Assert.Equal(f.LibraryId, top[0].LibraryId);

        var missingParent = await Assert.ThrowsAsync<SeafException>(() => f.Session.Directories.CreateAsync(f.LibraryId, root + "/x/y"));
        Assert.True(missingParent.IsNotFound);
        var withParents = await f.Session.Directories.CreateAsync(f.LibraryId, root + "/x/y", createParents: true);
        Assert.Equal(root + "/x/y", withParents.Path);
    }

    [LiveFact]
    public async Task List_Filters_Recursive_And_Missing()
    {
        var root = Root();
        await f.Session.Directories.EnsureAsync(f.LibraryId, root + "/sub/inner");
        await f.Session.Files.UploadAsync(f.LibraryId, root, "top.txt", new MemoryStream("x"u8.ToArray()));
        await f.Session.Files.UploadAsync(f.LibraryId, root + "/sub/inner", "deep.txt", new MemoryStream("y"u8.ToArray()));

        var files = await f.Session.Directories.ListAsync(f.LibraryId, root, SeafListFilter.FilesOnly);
        Assert.Equal(["top.txt"], files.Select(e => e.Name));
        Assert.All(files, e => Assert.NotNull(e.Size));

        var dirs = await f.Session.Directories.ListAsync(f.LibraryId, root, SeafListFilter.DirectoriesOnly);
        Assert.Equal(["sub"], dirs.Select(e => e.Name));

        var everything = await f.Session.Directories.ListAsync(f.LibraryId, root, recursive: true);
        Assert.Contains(everything, e => e.Path == root + "/sub/inner/deep.txt" && e.IsFile);
        Assert.Contains(everything, e => e.Path == root + "/sub/inner" && e.IsDirectory);

        var viaEntry = await f.Session.Directories.ListAsync(dirs[0]);
        Assert.Equal(["inner"], viaEntry.Select(e => e.Name));

        var missing = await Assert.ThrowsAsync<SeafException>(() => f.Session.Directories.ListAsync(f.LibraryId, root + "/nope"));
        Assert.True(missing.IsNotFound);
        Assert.Null(await f.Session.Directories.TryGetAsync(f.LibraryId, root + "/nope"));
    }

    [LiveFact]
    public async Task Rename_Copy_Move_Delete()
    {
        var root = Root();
        await f.Session.Directories.EnsureAsync(f.LibraryId, root + "/src/child");
        await f.Session.Directories.EnsureAsync(f.LibraryId, root + "/dst");

        var renamed = await f.Session.Directories.RenameAsync(f.LibraryId, root + "/src", "src2");
        Assert.Equal(root + "/src2", renamed.Path);

        await f.Session.Directories.CopyAsync(f.LibraryId, root + "/src2", f.LibraryId, root + "/dst");
        Assert.True(await f.Session.Directories.ExistsAsync(f.LibraryId, root + "/dst/src2/child"));
        Assert.True(await f.Session.Directories.ExistsAsync(f.LibraryId, root + "/src2"));

        await f.Session.Directories.DeleteAsync(f.LibraryId, root + "/dst/src2");
        await f.Session.Directories.MoveAsync(f.LibraryId, root + "/src2", f.LibraryId, root + "/dst");
        Assert.False(await f.Session.Directories.ExistsAsync(f.LibraryId, root + "/src2"));
        Assert.True(await f.Session.Directories.ExistsAsync(f.LibraryId, root + "/dst/src2/child"));

        Assert.True(await f.Session.Directories.DeleteIfExistsAsync(f.LibraryId, root + "/dst"));
        Assert.False(await f.Session.Directories.DeleteIfExistsAsync(f.LibraryId, root + "/dst"));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Session.Directories.DeleteAsync(f.LibraryId, "/"));
    }
}

[Collection(LiveCollection.Name)]
public class FileTests(LiveFixture f)
{
    private string Root([System.Runtime.CompilerServices.CallerMemberName] string name = "") => "/files-" + name;

    [LiveFact]
    public async Task Upload_Download_UnicodeRoundTrip()
    {
        var root = Root();
        await f.Session.Directories.EnsureAsync(f.LibraryId, root);
        const string text = "Grüße, 世界 — tax season ✓";
        long lastProgress = 0;
        var progress = new SyncProgress(v => lastProgress = v);

        var up = await f.Session.Files.UploadAsync(f.LibraryId, root, "résumé 2026.txt", new MemoryStream(Encoding.UTF8.GetBytes(text)), new SeafUploadOptions { Progress = progress });
        Assert.Equal("résumé 2026.txt", up.Name);
        Assert.Equal(root + "/résumé 2026.txt", up.Path);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), up.Size);
        Assert.Equal(up.Size, lastProgress);

        Assert.Equal(text, await f.Session.Files.ReadAllTextAsync(f.LibraryId, up.Path));

        var detail = await f.Session.Files.GetAsync(f.LibraryId, up.Path);
        Assert.Equal(up.Size, detail.Size);
        Assert.Equal(up.Id, detail.Id);
        Assert.Equal(root, detail.ParentDir);
        Assert.NotNull(detail.Modified);

        var link = await f.Session.Files.GetDownloadLinkAsync(f.LibraryId, up.Path, reuse: true);
        using var http = new HttpClient();
        Assert.Equal(text, Encoding.UTF8.GetString(await http.GetByteArrayAsync(link)));
        Assert.Equal(text, Encoding.UTF8.GetString(await http.GetByteArrayAsync(link)));
    }

    [LiveFact]
    public async Task Upload_AutoRename_Replace_CreateParents()
    {
        var root = Root();
        await f.Session.Directories.EnsureAsync(f.LibraryId, root);
        var a = await f.Session.Files.UploadAsync(f.LibraryId, root, "dup.txt", new MemoryStream("one"u8.ToArray()));
        var b = await f.Session.Files.UploadAsync(f.LibraryId, root, "dup.txt", new MemoryStream("two"u8.ToArray()));
        Assert.Equal("dup.txt", a.Name);
        Assert.NotEqual("dup.txt", b.Name);
        Assert.Equal("two", await f.Session.Files.ReadAllTextAsync(f.LibraryId, b.Path));

        var c = await f.Session.Files.UploadAsync(f.LibraryId, root, "dup.txt", new MemoryStream("three"u8.ToArray()), new SeafUploadOptions { Replace = true });
        Assert.Equal("dup.txt", c.Name);
        Assert.Equal("three", await f.Session.Files.ReadAllTextAsync(f.LibraryId, root + "/dup.txt"));

        var nested = await f.Session.Files.UploadAsync(f.LibraryId, root + "/new/deeper", "n.txt", new MemoryStream("n"u8.ToArray()), new SeafUploadOptions { CreateParents = true });
        Assert.Equal(root + "/new/deeper/n.txt", nested.Path);
        Assert.True(await f.Session.Files.ExistsAsync(f.LibraryId, nested.Path));

        var noParent = await Assert.ThrowsAsync<SeafException>(() => f.Session.Files.UploadAsync(f.LibraryId, root + "/missing", "m.txt", new MemoryStream("m"u8.ToArray())));
        Assert.True(noParent.IsNotFound || noParent.StatusCode == HttpStatusCode.BadRequest);
    }

    [LiveFact]
    public async Task Update_History_Rename_Copy_Move_Delete()
    {
        var root = Root();
        await f.Session.Directories.EnsureAsync(f.LibraryId, root + "/other");
        var up = await f.Session.Files.UploadAsync(f.LibraryId, root, "f.txt", new MemoryStream("v1"u8.ToArray()));

        var newId = await f.Session.Files.UpdateAsync(f.LibraryId, up.Path, new MemoryStream("version 2"u8.ToArray()));
        Assert.NotEqual(up.Id, newId);
        Assert.Equal("version 2", await f.Session.Files.ReadAllTextAsync(f.LibraryId, up.Path));
        // History is indexed asynchronously (and quick revisions may be coalesced), so poll and only assert it shows up.
        IReadOnlyList<SeafFileRevision> history = [];
        for (var i = 0; i < 20 && history.Count == 0; i++)
        {
            history = await f.Session.Files.GetHistoryAsync(f.LibraryId, up.Path);
            if (history.Count == 0) await Task.Delay(500);
        }
        Assert.NotEmpty(history);
        Assert.Equal(up.Path, history[0].Path);

        var renamed = await f.Session.Files.RenameAsync(f.LibraryId, up.Path, "g.txt");
        Assert.Equal(root + "/g.txt", renamed.Path);

        var copy = await f.Session.Files.CopyAsync(f.LibraryId, renamed.Path, f.LibraryId, root + "/other");
        Assert.Equal(root + "/other/g.txt", copy.Path);

        var moved = await f.Session.Files.MoveAsync(f.LibraryId, renamed.Path, f.Library.Id, root + "/other");
        Assert.NotEqual(root + "/other/g.txt", moved.Path); // copy already took the name; Seafile auto-renames
        Assert.False(await f.Session.Files.ExistsAsync(f.LibraryId, renamed.Path));

        var empty = await f.Session.Files.CreateAsync(f.LibraryId, root + "/empty.md");
        Assert.Equal(0, empty.Size);

        await f.Session.Files.DeleteAsync(f.LibraryId, empty.Path);
        Assert.Null(await f.Session.Files.TryGetAsync(f.LibraryId, empty.Path));
        await f.Session.Files.DeleteAsync(f.LibraryId, empty.Path); // idempotent

        await f.Session.Items.DeleteAsync(f.LibraryId, root + "/other", ["g.txt", moved.Name]);
        Assert.Empty(await f.Session.Directories.ListAsync(f.LibraryId, root + "/other"));
    }

    [LiveFact]
    public async Task OpenRead_Streams_And_LockUnlock()
    {
        var root = Root();
        await f.Session.Directories.EnsureAsync(f.LibraryId, root);
        var payload = new byte[300_000];
        Random.Shared.NextBytes(payload);
        var up = await f.Session.Files.UploadAsync(f.LibraryId, root, "blob.bin", new MemoryStream(payload));

        await using (var s = await f.Session.Files.OpenReadAsync(f.LibraryId, up.Path))
        {
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            Assert.Equal(payload, ms.ToArray());
        }

        if ((await f.Session.Server.GetInfoAsync()).IsPro)
        {
            await f.Session.Files.LockAsync(f.LibraryId, up.Path);
            Assert.True((await f.Session.Files.GetAsync(f.LibraryId, up.Path)).IsLocked
                || (await f.Session.Directories.ListAsync(f.LibraryId, root)).Single().IsLocked);
            await f.Session.Files.UnlockAsync(f.LibraryId, up.Path);
            Assert.False((await f.Session.Directories.ListAsync(f.LibraryId, root)).Single().IsLocked);
        }
    }

    [LiveFact]
    public async Task Thumbnail_ForPng()
    {
        var root = Root();
        await f.Session.Directories.EnsureAsync(f.LibraryId, root);
        var up = await f.Session.Files.UploadAsync(f.LibraryId, root, "dot.png", new MemoryStream(TinyPng), new SeafUploadOptions { ContentType = "image/png" });
        var thumb = await f.Session.Files.GetThumbnailAsync(f.LibraryId, up.Path, 48);
        Assert.True(thumb.Length > 8);
    }

    // 2x2 red PNG
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFklEQVR4nGP4z8DAwMDAxMDAwMDAAAANHQEDasKb6QAAAABJRU5ErkJggg==");

    /// <summary>Progress&lt;T&gt; posts asynchronously; this reports inline so assertions see the final value.</summary>
    private sealed class SyncProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }
}

[Collection(LiveCollection.Name)]
public class SharingTests(LiveFixture f)
{
    [LiveFact]
    public async Task ShareLinks_CreateListGetOrCreateDelete()
    {
        await f.Session.Directories.EnsureAsync(f.LibraryId, "/share/dir");
        var file = await f.Session.Files.UploadAsync(f.LibraryId, "/share", "s.txt", new MemoryStream("s"u8.ToArray()));

        var dirLink = await f.Session.ShareLinks.CreateAsync(f.LibraryId, "/share/dir", new SeafShareLinkOptions { ExpireDays = 2, CanUpload = true });
        Assert.True(dirLink.IsDir);
        Assert.True(dirLink.Permissions.CanUpload);
        Assert.NotNull(dirLink.ExpiresAt);
        Assert.StartsWith("http", dirLink.Link);

        var expires = DateTimeOffset.UtcNow.AddHours(3);
        var fileLink = await f.Session.ShareLinks.CreateAsync(f.LibraryId, file.Path, new SeafShareLinkOptions { ExpiresAt = expires, Password = "alphagamma1", CanDownload = true });
        Assert.True(fileLink.HasPassword);
        Assert.InRange(fileLink.ExpiresAt!.Value, expires.AddMinutes(-1), expires.AddMinutes(1));

        var listed = await f.Session.ShareLinks.ListAsync(f.LibraryId, "/share/dir");
        Assert.Contains(listed, l => l.Token == dirLink.Token);
        Assert.Equal(fileLink.Token, (await f.Session.ShareLinks.GetAsync(fileLink.Token)).Token);

        var (same, created) = await f.Session.ShareLinks.GetOrCreateAsync(f.LibraryId, file.Path);
        Assert.False(created);
        Assert.Equal(fileLink.Token, same.Token);

        await f.Session.ShareLinks.DeleteAsync(fileLink.Token);
        var (fresh, createdNow) = await f.Session.ShareLinks.GetOrCreateAsync(f.LibraryId, file.Path, new SeafShareLinkOptions { ExpireDays = 1 });
        Assert.True(createdNow);
        Assert.NotEqual(fileLink.Token, fresh.Token);

        await f.Session.ShareLinks.DeleteAsync(fresh.Token);
        await f.Session.ShareLinks.DeleteAsync(dirLink.Token);
        Assert.Empty(await f.Session.ShareLinks.ListAsync(f.LibraryId, "/share/dir"));
    }

    [LiveFact]
    public async Task SmartLinks()
    {
        await f.Session.Directories.EnsureAsync(f.LibraryId, "/smart");
        var file = await f.Session.Files.UploadAsync(f.LibraryId, "/smart", "x.txt", new MemoryStream("x"u8.ToArray()));
        var dir = await f.Session.ShareLinks.GetSmartLinkAsync(f.LibraryId, "/smart", true);
        Assert.Contains("/smart-link/", dir.SmartLink);
        var fl = await f.Session.ShareLinks.GetSmartLinkAsync(f.LibraryId, file.Path, false);
        Assert.Equal("x.txt", fl.Name);
    }

    [LiveFact]
    public async Task Starred()
    {
        var file = await f.Session.Files.UploadAsync(f.LibraryId, "/", "star.txt", new MemoryStream("*"u8.ToArray()));
        await f.Session.Starred.StarAsync(f.LibraryId, file.Path);
        Assert.Contains(await f.Session.Starred.ListAsync(), s => s.LibraryId == f.LibraryId && s.Path == file.Path);
        await f.Session.Starred.UnstarAsync(f.LibraryId, file.Path);
        Assert.DoesNotContain(await f.Session.Starred.ListAsync(), s => s.LibraryId == f.LibraryId && s.Path == file.Path);
    }

    [LiveFact]
    public async Task Groups_And_LibraryShares()
    {
        var group = await f.Session.Groups.CreateAsync($"SeafClientTest {Guid.NewGuid():N}"[..30]);
        try
        {
            Assert.Contains(await f.Session.Groups.ListAsync(), g => g.Id == group.Id);
            var renamed = await f.Session.Groups.RenameAsync(group.Id, group.Name + "R");
            Assert.Equal(group.Name + "R", renamed.Name);

            var members = await f.Session.Groups.ListMembersAsync(group.Id);
            var me = Assert.Single(members);
            Assert.Equal("Owner", me.Role);

            var bulk = await f.Session.Groups.AddMembersAsync(group.Id, ["nobody-" + Guid.NewGuid().ToString("N") + "@example.com"]);
            Assert.Single(bulk.Failed);

            await f.Session.Libraries.ShareWithGroupAsync(f.LibraryId, group.Id, SeafSharePermission.R);
            var shares = await f.Session.Libraries.ListSharesAsync(f.LibraryId);
            Assert.Contains(shares, s => s.ShareType == "group" && s.GroupInfo?.Id == group.Id && s.Permission == "r");
            Assert.Contains(await f.Session.Groups.ListLibrariesAsync(group.Id), l => l.Id == f.LibraryId);

            await f.Session.Libraries.UnshareFromGroupAsync(f.LibraryId, group.Id);
            Assert.DoesNotContain(await f.Session.Libraries.ListSharesAsync(f.LibraryId), s => s.GroupInfo?.Id == group.Id);
        }
        finally
        {
            await f.Session.Groups.DeleteAsync(group.Id);
        }
    }
}

[Collection(LiveCollection.Name)]
public class AdminAndMetadataTests(LiveFixture f)
{
    [LiveFact]
    public async Task AdminUsers_ListGetSearch()
    {
        var account = await f.Session.Server.GetAccountInfoAsync();
        if (!account.IsStaff) return;

        var page = await f.Session.Admin.Users.ListAsync(1, 2);
        Assert.True(page.TotalCount >= 1);
        Assert.InRange(page.Items.Count, 1, 2);

        var all = new List<SeafUser>();
        await foreach (var u in f.Session.Admin.Users.ListAllAsync(perPage: 1)) all.Add(u);
        Assert.Equal(page.TotalCount, all.Count);

        var me = await f.Session.Admin.Users.GetAsync(account.Email);
        Assert.True(me.IsStaff);
        Assert.True(me.IsActive);
        Assert.NotNull(me.Created);

        Assert.Null(await f.Session.Admin.Users.TryGetAsync("nobody-" + Guid.NewGuid().ToString("N") + "@example.com"));
        Assert.Contains(await f.Session.Admin.Users.SearchAsync(account.Name), u => u.Email == account.Email);
    }

    [LiveFact]
    public async Task Metadata_StatusEnableRecord()
    {
        if (!(await f.Session.Server.GetInfoAsync()).IsPro) return;

        Assert.False((await f.Session.Metadata.GetStatusAsync(f.LibraryId)).Enabled);
        await f.Session.Metadata.SetEnabledAsync(f.LibraryId, true);
        Assert.True((await f.Session.Metadata.GetStatusAsync(f.LibraryId)).Enabled);

        await f.Session.Directories.EnsureAsync(f.LibraryId, "/meta");

        // Indexing is asynchronous; poll briefly for the record to appear.
        SeafMetadataRecord? record = null;
        for (var i = 0; i < 20 && record == null; i++)
        {
            record = await f.Session.Metadata.GetRecordAsync(f.LibraryId, "/meta");
            if (record == null) await Task.Delay(500);
        }

        if (record != null)
        {
            Assert.Equal("meta", record.Name);
            await f.Session.Metadata.UpdateRecordAsync(f.LibraryId, record.Id, new Dictionary<string, object?> { ["_description"] = "Account 42" });
            Assert.Equal("Account 42", (await f.Session.Metadata.GetRecordAsync(f.LibraryId, "/meta"))!.Description);
        }

        await f.Session.Metadata.SetEnabledAsync(f.LibraryId, false);
        Assert.False((await f.Session.Metadata.GetStatusAsync(f.LibraryId)).Enabled);
    }
}
