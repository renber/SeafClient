using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace SeafClient.Tests;

/// <summary>Ownership, history, snapshots, trash, labels and shares-with-me. Each test uses its own throwaway library.</summary>
[Collection(LiveCollection.Name)]
public class LibraryHistoryTests(LiveFixture f)
{
    private SeafSession S => f.Session;

    private async Task<SeafLibrary> TempLibrary() => await S.Libraries.CreateAsync($"SeafClientHist-{Guid.NewGuid():N}"[..26]);

    private Task Upload(string lib, string dir, string name, string text)
        => S.Files.UploadAsync(lib, dir, name, new MemoryStream(Encoding.UTF8.GetBytes(text)), new SeafUploadOptions { CreateParents = true });

    private async Task<string> Head(string lib) => (await S.Libraries.GetHistoryAsync(lib, 1, 1)).Items[0].CommitId;

    [LiveFact]
    public async Task History_Commit_Revision_Revert()
    {
        var lib = await TempLibrary();
        try
        {
            await Upload(lib.Id, "/d", "f.txt", "one");
            var c1 = await Head(lib.Id);

            var history = await S.Libraries.GetHistoryAsync(lib.Id, 1, 50);
            Assert.Contains(history.Items, c => c.CommitId == c1 && c.Description.Contains("f.txt"));
            Assert.Contains(history.Items, c => c.Description == "Created library");
            Assert.False(history.HasMore);

            var detail = await S.Libraries.GetCommitAsync(lib.Id, c1);
            Assert.Contains(detail.Changes, d => d.Path == "/d/f.txt" && d.OpType == "new");
            Assert.Equal(LiveServer.User, detail.Info.CreatorEmail, ignoreCase: true);

            await S.Files.UpdateAsync(lib.Id, "/d/f.txt", new MemoryStream("two"u8.ToArray()));
            Assert.Equal("one", await S.Files.ReadRevisionTextAsync(lib.Id, "/d/f.txt", c1));

            await S.Files.RevertAsync(lib.Id, "/d/f.txt", c1);
            Assert.Equal("one", await S.Files.ReadAllTextAsync(lib.Id, "/d/f.txt"));

            // Snapshot listing and directory revert after deleting the folder.
            await S.Directories.DeleteAsync(lib.Id, "/d");
            var snap = await S.Libraries.ListSnapshotDirectoryAsync(lib.Id, c1, "/d");
            var entry = Assert.Single(snap);
            Assert.Equal("/d/f.txt", entry.Path);
            Assert.True(entry.IsFile);

            await S.Directories.RevertAsync(lib.Id, "/d", c1);
            Assert.True(await S.Files.ExistsAsync(lib.Id, "/d/f.txt"));

            // Whole-library revert to the first upload drops later additions.
            await Upload(lib.Id, "/", "later.txt", "x");
            await S.Libraries.RevertToSnapshotAsync(lib.Id, c1);
            Assert.False(await S.Files.ExistsAsync(lib.Id, "/later.txt"));
            Assert.Equal("one", await S.Files.ReadAllTextAsync(lib.Id, "/d/f.txt"));
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task Trash_List_Restore_Clean()
    {
        var lib = await TempLibrary();
        try
        {
            await Upload(lib.Id, "/t", "gone.txt", "bye");
            await S.Directories.EnsureAsync(lib.Id, "/t/sub");
            await S.Files.DeleteAsync(lib.Id, "/t/gone.txt");
            await S.Directories.DeleteAsync(lib.Id, "/t/sub");

            var trash = await S.Libraries.ListTrashAsync(lib.Id, "/t");
            var file = Assert.Single(trash.Items, i => i.ObjName == "gone.txt");
            Assert.False(file.IsDir);
            Assert.Equal(3, file.Size);
            Assert.Equal("/t/gone.txt", file.Path);
            var dir = Assert.Single(trash.Items, i => i.ObjName == "sub");
            Assert.True(dir.IsDir);
            Assert.Null(dir.Size); // server sends "" for directories

            await S.Libraries.RestoreFromTrashAsync(lib.Id, file);
            Assert.Equal("bye", await S.Files.ReadAllTextAsync(lib.Id, "/t/gone.txt"));

            var partial = await S.Libraries.RestoreFromTrashAsync(lib.Id, dir.CommitId, [dir.Path, "/t/never-existed.txt"]);
            Assert.Single(partial.Success, s => s.Path == "/t/sub" && s.IsDir);
            Assert.Single(partial.Failed);
            Assert.True(await S.Directories.ExistsAsync(lib.Id, "/t/sub"));

            await S.Files.DeleteAsync(lib.Id, "/t/gone.txt");
            Assert.NotEmpty((await S.Libraries.ListTrashAsync(lib.Id)).Items);
            await S.Libraries.CleanTrashAsync(lib.Id);
            Assert.Empty((await S.Libraries.ListTrashAsync(lib.Id)).Items);

            var missing = await Assert.ThrowsAsync<SeafException>(() => S.Libraries.ListTrashAsync(lib.Id, "/no-such-folder"));
            Assert.True(missing.IsNotFound);
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task HistoryLimit_GetSet()
    {
        var lib = await TempLibrary();
        try
        {
            Assert.Equal(-1, await S.Libraries.GetHistoryLimitAsync(lib.Id)); // default: keep all
            Assert.Equal(30, await S.Libraries.SetHistoryLimitAsync(lib.Id, 30));
            Assert.Equal(30, await S.Libraries.GetHistoryLimitAsync(lib.Id));
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task SnapshotLabels()
    {
        var lib = await TempLibrary();
        var l1 = $"sc-{Guid.NewGuid():N}"[..11];
        var l2 = l1 + ".b";
        try
        {
            await Upload(lib.Id, "/", "a.txt", "a");
            var head = await Head(lib.Id);

            var added = await S.Libraries.AddSnapshotLabelsAsync(lib.Id, [l1]);
            var label = Assert.Single(added);
            Assert.Equal(l1, label.Name);
            Assert.Equal(head, label.Revision.CommitId);
            Assert.Equal(lib.Id, label.Revision.LibraryId);

            Assert.Contains(l1, await S.Libraries.ListSnapshotLabelNamesAsync());

            var replaced = await S.Libraries.SetSnapshotLabelsAsync(lib.Id, [l2], head);
            Assert.Equal([l2], replaced.Select(r => r.Name));
            Assert.Contains(l2, await S.Libraries.ListSnapshotLabelNamesAsync());
            Assert.DoesNotContain(l1, await S.Libraries.ListSnapshotLabelNamesAsync());

            await S.Libraries.DeleteSnapshotLabelAsync(lib.Id, l2);
            Assert.DoesNotContain(l2, await S.Libraries.ListSnapshotLabelNamesAsync());

            var bad = await Assert.ThrowsAsync<SeafException>(() => S.Libraries.AddSnapshotLabelsAsync(lib.Id, ["no spaces"]));
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task SearchByName()
    {
        var token = f.Library.Name["SeafClientTests-".Length..];
        var found = await S.Libraries.SearchByNameAsync(token.ToUpperInvariant());
        Assert.Equal(f.LibraryId, Assert.Single(found).Id);
        Assert.Empty(await S.Libraries.SearchByNameAsync("no-library-has-this-name-" + Guid.NewGuid().ToString("N")));
    }

    /// <summary>
    /// Transfer a library to another user, have an admin share it back, then leave the share.
    /// Uses raw admin calls for setup/cleanup so the test only depends on the APIs under test.
    /// </summary>
    [LiveFact]
    public async Task Owner_Transfer_SharedWithMe_Leave()
    {
        var other = await FirstOtherUser();
        Assert.True(other != null, "Needs a second user on the server.");

        var lib = await TempLibrary();
        var transferred = false;
        try
        {
            Assert.Equal(LiveServer.User, await S.Libraries.GetOwnerAsync(lib.Id), ignoreCase: true);

            await S.Libraries.TransferAsync(lib.Id, other!);
            transferred = true;
            // Even a system admin may not read the owner of a library they no longer own.
            var denied = await Assert.ThrowsAsync<SeafException>(() => S.Libraries.GetOwnerAsync(lib.Id));
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            using (var http = AdminHttp())
            {
                var r = await http.PostAsync("api/v2.1/admin/shares/", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["repo_id"] = lib.Id, ["share_type"] = "user", ["share_to"] = LiveServer.User!, ["permission"] = "r",
                }));
                r.EnsureSuccessStatusCode();
            }

            var shared = Assert.Single(await S.Libraries.ListSharedWithMeAsync(), s => s.Id == lib.Id);
            Assert.Equal("personal", shared.ShareType);
            Assert.Equal("r", shared.Permission);
            Assert.Equal(other, shared.SharedBy);
            Assert.Equal(lib.Name, shared.Name);

            await S.Libraries.LeaveShareAsync(shared);
            Assert.DoesNotContain(await S.Libraries.ListSharedWithMeAsync(), s => s.Id == lib.Id);
        }
        finally
        {
            if (transferred)
            {
                using var http = AdminHttp();
                (await http.DeleteAsync($"api/v2.1/admin/libraries/{lib.Id}/")).EnsureSuccessStatusCode();
            }
            else
            {
                await S.Libraries.DeleteAsync(lib.Id);
            }
        }
    }

    private async Task<string?> FirstOtherUser()
    {
        var page = await S.Admin.Users.ListAsync(1, 50);
        return page.Items.FirstOrDefault(u => u.IsActive && !u.Email.Equals(LiveServer.User, StringComparison.OrdinalIgnoreCase))?.Email;
    }

    private HttpClient AdminHttp()
    {
        var http = new HttpClient { BaseAddress = S.ServerUri };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", S.AuthToken);
        return http;
    }
}
