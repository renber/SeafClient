using System.Net;
using System.Text;

namespace SeafClient.Tests;

/// <summary>Repo API token management and the via-repo-token API. Uses its own library and revokes its tokens.</summary>
[Collection(LiveCollection.Name)]
public class RepoTokenTests(LiveFixture f) : IAsyncLifetime
{
    private SeafLibrary _lib = null!;
    private string Lib => _lib.Id;
    private SeafSession S => f.Session;

    public async Task InitializeAsync()
    {
        if (!LiveServer.IsConfigured) return;
        _lib = await f.Session.Libraries.CreateAsync($"SeafClientRT-{Guid.NewGuid():N}"[..25]);
    }

    public async Task DisposeAsync()
    {
        if (_lib == null) return;
        foreach (var t in await S.Libraries.ListApiTokensAsync(Lib))
            await S.Libraries.DeleteApiTokenAsync(Lib, t.AppName);
        await S.Libraries.DeleteAsync(Lib);
    }

    private SeafRepoTokenSession Open(string token) => SeafRepoTokenSession.FromToken(new Uri(LiveServer.Url!), token);

    [LiveFact]
    public async Task Tokens_CreateListUpdateDelete()
    {
        var token = await S.Libraries.CreateApiTokenAsync(Lib, "seafclient app/1", SeafRepoTokenPermission.R);
        Assert.Equal(Lib, token.LibraryId);
        Assert.Equal(SeafRepoTokenPermission.R, token.Permission);
        Assert.Equal(40, token.ApiToken.Length);

        var dup = await Assert.ThrowsAsync<SeafException>(() => S.Libraries.CreateApiTokenAsync(Lib, "seafclient app/1"));
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);

        var listed = Assert.Single(await S.Libraries.ListApiTokensAsync(Lib));
        Assert.Equal(token.ApiToken, listed.ApiToken);
        Assert.Equal(LiveServer.User, listed.GeneratedBy, ignoreCase: true);

        var updated = await S.Libraries.UpdateApiTokenAsync(Lib, "seafclient app/1", SeafRepoTokenPermission.Rw);
        Assert.Equal(SeafRepoTokenPermission.Rw, updated.Permission);
        Assert.Equal(token.ApiToken, updated.ApiToken);

        using (var repo = Open(token.ApiToken))
        {
            Assert.True(await repo.PingAsync());
            var info = await repo.GetInfoAsync();
            Assert.Equal(Lib, info.Id);
            Assert.Equal(_lib.Name, info.Name);
        }

        await S.Libraries.DeleteApiTokenAsync(Lib, "seafclient app/1");
        Assert.Empty(await S.Libraries.ListApiTokensAsync(Lib));
        using var revoked = Open(token.ApiToken);
        Assert.False(await revoked.PingAsync());
    }

    [LiveFact]
    public async Task ReadOnlyToken_CannotWrite()
    {
        var token = await S.Libraries.CreateApiTokenAsync(Lib, "readonly", SeafRepoTokenPermission.R);
        using var repo = Open(token.ApiToken);
        Assert.Empty(await repo.Directories.ListAsync());
        var ex = await Assert.ThrowsAsync<SeafException>(() => repo.Files.CreateAsync("/x.txt"));
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [LiveFact]
    public async Task DirectoriesFilesItems()
    {
        var token = await S.Libraries.CreateApiTokenAsync(Lib, "rw");
        using var repo = Open(token.ApiToken);

        // Directories.
        var dir = await repo.Directories.CreateAsync("/docs");
        Assert.Equal("/docs", dir.Path);
        Assert.True((await Assert.ThrowsAsync<SeafException>(() => repo.Directories.CreateAsync("/docs"))).IsConflict);
        Assert.True((await Assert.ThrowsAsync<SeafException>(() => repo.Directories.CreateAsync("/missing/child"))).IsNotFound);
        await repo.Directories.EnsureAsync("/docs/a/b/c");
        await repo.Directories.EnsureAsync("/docs/a/b/c");
        Assert.True(await repo.Directories.ExistsAsync("/docs/a/b/c"));
        Assert.Single(await repo.Directories.ListAsync("/docs"), e => e.Name.StartsWith('a'));
        var renamed = await repo.Directories.RenameAsync("/docs/a", "alpha");
        Assert.Equal("/docs/alpha", renamed.Path);

        // Upload / download.
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes("héllo")))
        {
            var up = await repo.Files.UploadAsync("/docs", "hello.txt", ms);
            Assert.Equal("/docs/hello.txt", up.Path);
            Assert.Equal(Lib, up.LibraryId);
        }
        Assert.Equal("héllo", await repo.Files.ReadAllTextAsync("/docs/hello.txt"));
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes("v2")))
            Assert.Equal("hello.txt", (await repo.Files.UploadAsync("/docs", "hello.txt", ms, new SeafUploadOptions { Replace = true })).Name);
        Assert.Equal("v2", await repo.Files.ReadAllTextAsync("/docs/hello.txt"));
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes("deep")))
            Assert.Equal("/new/deep/d.txt", (await repo.Files.UploadAsync("/new/deep", "d.txt", ms, new SeafUploadOptions { CreateParents = true })).Path);

        // Listing (recursive, filtered) fills in the library id.
        var files = await repo.Directories.ListAsync("/docs", SeafListFilter.FilesOnly, recursive: true);
        var hello = Assert.Single(files);
        Assert.Equal("/docs/hello.txt", hello.Path);
        Assert.Equal(Lib, hello.LibraryId);
        Assert.Equal(2, hello.Size);
        Assert.All(await repo.Directories.ListAsync("/docs", SeafListFilter.DirectoriesOnly, recursive: true), e => Assert.True(e.IsDirectory));

        // File operations.
        var info = await repo.Files.GetAsync("/docs/hello.txt");
        Assert.Equal(2, info.Size);
        Assert.Null(await repo.Files.TryGetAsync("/docs/nope.txt"));
        var created = await repo.Files.CreateAsync("/docs/empty.txt");
        Assert.Equal("/docs/empty.txt", created.Path);
        Assert.Equal("/docs/empty (1).txt", (await repo.Files.CreateAsync("/docs/empty.txt")).Path);
        Assert.Equal("/docs/e2.txt", (await repo.Files.RenameAsync("/docs/empty (1).txt", "e2.txt")).Path);
        Assert.Equal("/docs/alpha/e2.txt", (await repo.Files.CopyAsync("/docs/e2.txt", "/docs/alpha")).Path);
        Assert.Equal("/docs/alpha/e2 (1).txt", (await repo.Files.MoveAsync("/docs/e2.txt", "/docs/alpha")).Path);
        Assert.True((await repo.Files.LockAsync("/docs/hello.txt")).IsLocked);
        Assert.False((await repo.Files.UnlockAsync("/docs/hello.txt")).IsLocked);
        await repo.Files.DeleteAsync("/docs/alpha/e2 (1).txt");
        await repo.Files.DeleteAsync("/docs/alpha/e2 (1).txt"); // idempotent
        Assert.False(await repo.Files.ExistsAsync("/docs/alpha/e2 (1).txt"));

        // Batch items.
        await repo.Directories.EnsureAsync("/dst");
        await repo.Items.CopyAsync("/docs", ["hello.txt", "empty.txt"], "/dst");
        await repo.Items.MoveAsync("/dst", ["empty.txt"], "/docs/alpha");
        await repo.Items.DeleteAsync("/dst", ["hello.txt"]);
        Assert.Empty(await repo.Directories.ListAsync("/dst"));
        Assert.True(await repo.Files.ExistsAsync("/docs/alpha/empty.txt"));

        // Move with merge: /dst/alpha + /docs/alpha are merged instead of producing "alpha (1)".
        await repo.Directories.EnsureAsync("/dst/alpha");
        await repo.Files.CreateAsync("/dst/alpha/extra.txt");
        await repo.Directories.MoveMergeAsync("/dst/alpha", "/docs");
        Assert.True(await repo.Files.ExistsAsync("/docs/alpha/extra.txt"));
        Assert.True(await repo.Files.ExistsAsync("/docs/alpha/empty.txt"));
        Assert.False(await repo.Directories.ExistsAsync("/dst/alpha"));

        // Share link (owned by the token's creator).
        var link = await repo.ShareLinks.CreateAsync("/docs/hello.txt");
        Assert.False(link.IsDir);
        Assert.Contains(await S.ShareLinks.ListAsync(Lib), l => l.Token == link.Token);
        var dirLink = await repo.ShareLinks.CreateAsync("/docs", new SeafShareLinkPermissions { CanDownload = true, CanUpload = true });
        Assert.True(dirLink.IsDir);
        Assert.True(dirLink.Permissions.CanUpload);

        // Delete directories.
        await repo.Directories.DeleteAsync("/dst");
        Assert.False(await repo.Directories.DeleteIfExistsAsync("/dst"));
        Assert.True((await Assert.ThrowsAsync<SeafException>(() => repo.Directories.ListAsync("/dst"))).IsNotFound);
    }

    [LiveFact]
    public async Task Metadata_ViaToken()
    {
        if (!(await S.Server.GetInfoAsync()).IsPro) return;
        await S.Metadata.SetEnabledAsync(Lib, true);
        var token = await S.Libraries.CreateApiTokenAsync(Lib, "meta");
        using var repo = Open(token.ApiToken);

        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes("x")))
            await repo.Files.UploadAsync("/", "m.txt", ms);

        var views = await repo.Metadata.Views.ListAsync();
        var all = views.Views[0];
        SeafMetadataQueryResult records = new();
        for (var i = 0; i < 40 && records.Records.Count == 0; i++)
        {
            records = await repo.Metadata.ListRecordsAsync(all.Id);
            if (records.Records.Count == 0) await Task.Delay(500);
        }
        var record = Assert.Single(records.Records);

        await repo.Metadata.UpdateRecordAsync(record.Id, new Dictionary<string, object?> { ["_description"] = "via token" });
        Assert.Equal("via token", (await S.Metadata.GetRecordByIdAsync(Lib, record.Id))!.Description);

        var view = await repo.Metadata.Views.CreateAsync("TokView");
        await repo.Metadata.Views.RenameAsync(view.Id, "TokView2");
        Assert.Equal("TokView2", (await repo.Metadata.Views.GetAsync(view.Id)).Name);
        var copy = await repo.Metadata.Views.DuplicateAsync(view.Id);
        await repo.Metadata.Views.MoveAsync(copy.Id, all.Id);
        await repo.Metadata.Views.DeleteAsync(copy.Id);
        await repo.Metadata.Views.DeleteAsync(view.Id);
        Assert.Single((await repo.Metadata.Views.ListAsync()).Views);

        var tag = await repo.Metadata.Tags.CreateAsync("TokTag", "#00FF00");
        await repo.Metadata.Tags.UpdateAsync([new SeafMetadataTagUpdate(tag.Id, Name: "TokTag2")]);
        Assert.Equal("TokTag2", Assert.Single(await repo.Metadata.Tags.ListAsync()).Name);

        // Seafile 14.0 rejects file tagging through repo tokens per record; the result reports it instead of throwing.
        var result = await repo.Metadata.Tags.SetFileTagsAsync(new Dictionary<string, IEnumerable<string>> { [record.Id] = [tag.Id] });
        Assert.Contains(record.Id, result.Success.Concat(result.Fail));

        await repo.Metadata.Tags.DeleteAsync([tag.Id]);
        Assert.Empty(await repo.Metadata.Tags.ListAsync());
    }
}
