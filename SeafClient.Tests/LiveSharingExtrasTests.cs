using System.Net;
using System.Text;

namespace SeafClient.Tests;

/// <summary>Folder shares, batch library shares, sub-folder permissions, upload links and share-link extras.</summary>
[Collection(LiveCollection.Name)]
public class SharingExtrasTests(LiveFixture f)
{
    private SeafSession S => f.Session;

    /// <summary>Another existing user to share with (users cannot be created on the test server), or null.</summary>
    private async Task<string?> OtherUserAsync()
    {
        var me = (await S.Server.GetAccountInfoAsync()).Email;
        var page = await S.Admin.Users.ListAsync(1, 25);
        return page.Items.FirstOrDefault(u => !string.Equals(u.Email, me, StringComparison.OrdinalIgnoreCase) && u.IsActive)?.Email;
    }

    private async Task<T> WithTempLibrary<T>(Func<string, Task<T>> body)
    {
        var lib = await S.Libraries.CreateAsync($"SeafClientShare-{Guid.NewGuid():N}"[..28]);
        try { return await body(lib.Id); }
        finally { await S.Libraries.DeleteAsync(lib.Id); }
    }

    private async Task WithTempGroup(Func<int, Task> body)
    {
        var g = await S.Groups.CreateAsync($"SeafClientGrp-{Guid.NewGuid():N}"[..24]);
        try { await body(g.Id); }
        finally { await S.Groups.DeleteAsync(g.Id); }
    }

    [LiveFact]
    public async Task FolderShares_ShareUpdateListUnshare()
    {
        var other = await OtherUserAsync();
        Assert.NotNull(other);

        await WithTempLibrary<bool>(async lib =>
        {
            await S.Directories.CreateAsync(lib, "/shared");

            var result = await S.Libraries.ShareWithUsersAsync(lib, [other!, "nobody-seafclient@example.com"], SeafSharePermission.Rw, "/shared");
            Assert.Single(result.Success);
            Assert.Equal("rw", result.Success[0].Permission);
            Assert.Single(result.Failed);
            Assert.False(string.IsNullOrEmpty(result.Failed[0].ErrorMsg));
            Assert.False(string.IsNullOrEmpty(result.VirtualLibraryId));

            await S.Libraries.UpdateUserSharePermissionAsync(lib, other!, SeafSharePermission.R, "/shared");
            var shares = await S.Libraries.ListSharesAsync(lib, "/shared");
            Assert.Equal("r", Assert.Single(shares, s => s.ShareType == "user").Permission);

            var folders = await S.Libraries.ListSharedFoldersAsync();
            var folder = Assert.Single(folders, x => x.LibraryId == lib && x.Path == "/shared");
            Assert.Equal("personal", folder.ShareType);
            Assert.Equal(other, folder.UserEmail);
            Assert.Equal("r", folder.SharePermission);

            await WithTempGroup(async gid =>
            {
                var g = await S.Libraries.ShareWithGroupsAsync(lib, [gid], SeafSharePermission.R, "/shared");
                Assert.Single(g.Success);
                Assert.Empty(g.Failed);
                await S.Libraries.UpdateGroupSharePermissionAsync(lib, gid, SeafSharePermission.Rw, "/shared");
                var gf = Assert.Single(await S.Libraries.ListSharedFoldersAsync(), x => x.LibraryId == lib && x.IsGroupShare);
                Assert.Equal(gid, gf.GroupId);
                Assert.Equal("rw", gf.SharePermission);
                await S.Libraries.UnshareFromGroupAsync(lib, gid, "/shared");
            });

            await S.Libraries.UnshareFromUserAsync(lib, other!, "/shared");
            Assert.DoesNotContain(await S.Libraries.ListSharedFoldersAsync(), x => x.LibraryId == lib);
            return true;
        });
    }

    [LiveFact]
    public async Task BatchShare_ReportsPerLibraryResults()
    {
        var other = await OtherUserAsync();
        Assert.NotNull(other);

        await WithTempLibrary<bool>(async lib =>
        {
            var bogus = "00000000-0000-0000-0000-000000000000";
            await WithTempGroup(async gid =>
            {
                var shared = await S.Libraries.BatchShareWithGroupAsync([lib, bogus], gid, SeafSharePermission.R);
                var ok = Assert.Single(shared.Success);
                Assert.Equal(lib, ok.LibraryId);
                Assert.Equal(gid, ok.GroupId);
                Assert.Equal("r", ok.Permission);
                Assert.Equal(bogus, Assert.Single(shared.Failed).LibraryId);

                var unshared = await S.Libraries.BatchUnshareFromGroupAsync([lib], gid);
                Assert.Single(unshared.Success);
            });

            var u = await S.Libraries.BatchShareWithUserAsync([lib], other!, SeafSharePermission.Rw);
            Assert.Equal(other, Assert.Single(u.Success).Username);
            var again = await S.Libraries.BatchShareWithUserAsync([lib], other!);
            Assert.Single(again.Failed); // already shared

            Assert.Single((await S.Libraries.BatchUnshareFromUserAsync([lib], other!)).Success);
            Assert.Empty(await S.Libraries.ListSharesAsync(lib));
            return true;
        });
    }

    [LiveFact]
    public async Task FolderPermissions_UserAndGroup()
    {
        var other = await OtherUserAsync();
        Assert.NotNull(other);

        await WithTempLibrary<bool>(async lib =>
        {
            await S.Directories.CreateAsync(lib, "/perm");
            var fp = S.FolderPermissions;

            var added = await fp.AddUserPermissionAsync(lib, "/perm", other!, SeafSharePermission.R);
            Assert.Equal("/perm", added.FolderPath);
            Assert.Equal("r", added.Permission);
            await Assert.ThrowsAsync<SeafException>(() => fp.AddUserPermissionAsync(lib, "/perm", other!, SeafSharePermission.R));

            var updated = await fp.UpdateUserPermissionAsync(lib, "/perm", other!, SeafSharePermission.Rw);
            Assert.Equal("rw", updated.Permission);
            Assert.Equal(other, Assert.Single(await fp.ListUserPermissionsAsync(lib, "/perm")).UserEmail);
            Assert.Single(await fp.ListUserPermissionsAsync(lib));
            Assert.Empty(await fp.ListUserPermissionsAsync(lib, "/other"));

            var bulk = await fp.AddUserPermissionsAsync(lib, "/perm", ["nobody-seafclient@example.com"], SeafSharePermission.R);
            Assert.Single(bulk.Failed);

            await fp.RemoveUserPermissionAsync(lib, "/perm", other!);
            Assert.Empty(await fp.ListUserPermissionsAsync(lib));
            await fp.RemoveUserPermissionAsync(lib, "/perm", other!); // idempotent

            await WithTempGroup(async gid =>
            {
                var g = await fp.AddGroupPermissionAsync(lib, "/perm", gid, SeafSharePermission.R);
                Assert.Equal(gid, g.GroupId);
                Assert.Equal("rw", (await fp.UpdateGroupPermissionAsync(lib, "/perm", gid, SeafSharePermission.Rw)).Permission);
                Assert.Single(await fp.ListGroupPermissionsAsync(lib, "/perm"));
                await fp.RemoveGroupPermissionAsync(lib, "/perm", gid);
                Assert.Empty(await fp.ListGroupPermissionsAsync(lib));
            });

            await Assert.ThrowsAsync<ArgumentException>(() => fp.AddUserPermissionAsync(lib, "/perm", other!, SeafSharePermission.Admin));
            return true;
        });
    }

    [LiveFact]
    public async Task UploadLinks_Lifecycle_AndAnonymousUpload()
    {
        var dir = "/upload-links-" + Guid.NewGuid().ToString("N")[..8];
        await S.Directories.CreateAsync(f.LibraryId, dir);
        var ul = S.UploadLinks;

        var protectedLink = await ul.CreateAsync(f.LibraryId, dir, new SeafUploadLinkOptions { Password = "Upl0ad-Pass!", ExpireDays = 3 });
        Assert.True(protectedLink.HasPassword);
        Assert.Equal(dir + "/", protectedLink.Path);
        Assert.True(protectedLink.ExpiresAt > DateTimeOffset.UtcNow.AddDays(2.9));

        // One upload link per folder: creating again returns the existing link and ignores the options.
        var again = await ul.CreateAsync(f.LibraryId, dir);
        Assert.Equal(protectedLink.Token, again.Token);
        Assert.True(again.HasPassword);

        Assert.Equal(protectedLink.Token, Assert.Single(await ul.ListAsync(f.LibraryId, dir)).Token);
        Assert.Contains(await ul.ListAsync(), l => l.Token == protectedLink.Token);
        Assert.Equal(dir + "/", (await ul.GetAsync(protectedLink.Token)).Path);

        var expiry = DateTimeOffset.UtcNow.AddDays(20);
        var updated = await ul.UpdateAsync(protectedLink.Token, expiresAt: expiry);
        Assert.Equal(expiry.UtcDateTime, updated.ExpiresAt!.Value.UtcDateTime, TimeSpan.FromSeconds(1));

        var forbidden = await Assert.ThrowsAsync<SeafException>(() => ul.GetUploadUrlAsync(protectedLink.Token));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await ul.DeleteAsync(protectedLink.Token);
        await Assert.ThrowsAsync<SeafException>(() => ul.GetAsync(protectedLink.Token));

        var open = await ul.CreateAsync(f.LibraryId, dir);
        Assert.False(open.HasPassword);
        Assert.Contains("/seafhttp/", await ul.GetUploadUrlAsync(open.Token));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("via upload link"));
        var uploaded = await ul.UploadAsync(open.Token, "dropped.txt", content);
        Assert.Equal(dir + "/dropped.txt", uploaded.Path);
        Assert.Equal("via upload link", await S.Files.ReadAllTextAsync(f.LibraryId, uploaded.Path));

        try
        {
            var sent = await ul.SendAsync(open.Token, ["seafclient-test@example.com"], "hello");
            Assert.Contains("seafclient-test@example.com", sent.Success);
        }
        catch (SeafException e) when (e.StatusCode == HttpStatusCode.Forbidden && e.ServerMessage.Contains("email", StringComparison.OrdinalIgnoreCase))
        {
            // Server has no SMTP configured.
        }

        await ul.DeleteManyAsync([open.Token]);
        Assert.Empty(await ul.ListAsync(f.LibraryId, dir));
    }

    [LiveFact]
    public async Task ShareLinks_AdditionalLinks_Audiences_Update_Dirents_Batch()
    {
        var other = await OtherUserAsync();
        Assert.NotNull(other);
        var sl = S.ShareLinks;

        var dir = "/share-extras-" + Guid.NewGuid().ToString("N")[..8];
        await S.Directories.EnsureAsync(f.LibraryId, dir + "/sub");
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes("abc")))
            await S.Files.UploadAsync(f.LibraryId, dir, "a.txt", ms);

        var tokens = new List<string>();
        try
        {
            var restricted = await sl.CreateAdditionalAsync(f.LibraryId, dir, new SeafShareLinkOptions { ExpireDays = 2 },
                SeafShareLinkScope.SpecificUsers, [other!]);
            tokens.Add(restricted.Token);
            Assert.Equal("specific_users", restricted.UserScope);

            // A second link for the same folder is allowed through this endpoint.
            var second = await sl.CreateAdditionalAsync(f.LibraryId, dir);
            tokens.Add(second.Token);
            Assert.NotEqual(restricted.Token, second.Token);
            Assert.Equal(2, (await sl.ListAsync(f.LibraryId, dir)).Count);

            var users = await sl.ListAuthorizedUsersAsync(restricted.Token);
            Assert.Equal(other, Assert.Single(users).Username);
            await sl.RemoveAuthorizedUsersAsync(restricted.Token, [other!]);
            Assert.Empty(await sl.ListAuthorizedUsersAsync(restricted.Token, restricted.Path));
            var add = await sl.AddAuthorizedUsersAsync(restricted.Token, [other!, "nobody-seafclient@example.com"]);
            Assert.Single(add.Success);
            Assert.Equal("nobody-seafclient@example.com", Assert.Single(add.Failed).Email);

            var expiry = DateTimeOffset.UtcNow.AddDays(30);
            var updated = await sl.UpdateAsync(restricted.Token, new SeafShareLinkUpdate { ExpiresAt = expiry, CanDownload = false, Scope = SeafShareLinkScope.AllUsers });
            Assert.Equal(expiry.UtcDateTime, updated.ExpiresAt!.Value.UtcDateTime, TimeSpan.FromSeconds(1));
            Assert.False(updated.Permissions.CanDownload);
            Assert.Equal("all_users", updated.UserScope);

            var root = await sl.ListDirentsAsync(second.Token);
            Assert.Contains(root, d => d.IsDir && d.Name == "sub" && d.Path == "/sub");
            Assert.Contains(root, d => !d.IsDir && d.Name == "a.txt" && d.Size == 3 && d.Modified != null);
            Assert.Empty(await sl.ListDirentsAsync(second.Token, "/sub"));

            var batch = await sl.CreateBatchAsync(f.LibraryId, dir + "/a.txt", 2, autoGeneratePassword: true, new SeafShareLinkOptions { ExpireDays = 1 });
            tokens.AddRange(batch.Select(l => l.Token));
            Assert.Equal(2, batch.Count);
            Assert.All(batch, l => Assert.True(l.HasPassword));
            Assert.NotEqual(batch[0].Password, batch[1].Password);

            try
            {
                await sl.SendAsync(second.Token, ["seafclient-test@example.com"]);
            }
            catch (SeafException e) when (e.StatusCode == HttpStatusCode.Forbidden && e.ServerMessage.Contains("email", StringComparison.OrdinalIgnoreCase)) { }

            try
            {
                var emailLink = await sl.CreateAdditionalAsync(f.LibraryId, dir, scope: SeafShareLinkScope.SpecificEmails, authorized: ["a@example.com"]);
                tokens.Add(emailLink.Token);
                var r = await sl.AddAuthorizedEmailsAsync(emailLink.Token, ["b@example.com", "not-an-email"]);
                Assert.Contains("b@example.com", r.Success);
                Assert.Contains("b@example.com", await sl.ListAuthorizedEmailsAsync(emailLink.Token));
                await sl.RemoveAuthorizedEmailsAsync(emailLink.Token, ["b@example.com"]);
            }
            catch (SeafException e) when (e.StatusCode == HttpStatusCode.BadRequest && e.ServerMessage.Contains("not enabled", StringComparison.OrdinalIgnoreCase))
            {
                // Email authorization needs SMTP on the server.
            }
        }
        finally
        {
            await sl.DeleteManyAsync(tokens);
        }
        Assert.Empty(await sl.ListAsync(f.LibraryId, dir));
    }
}
