namespace SeafClient.Tests;

/// <summary>System admin API. Mutations only touch groups/libraries/departments created here (named "SeafClient...").</summary>
[Collection(LiveCollection.Name)]
public class AdminApiTests(LiveFixture f)
{
    private static string TempName(string kind) => $"SeafClient{kind}-{Guid.NewGuid():N}"[..28];

    /// <summary>Another existing user to add to temporary groups/shares (never modified itself).</summary>
    private async Task<string?> OtherUserAsync()
    {
        var page = await f.Session.Admin.Users.ListAsync(1, 20);
        return page.Items.Select(u => u.Email).FirstOrDefault(e => !e.Equals(LiveServer.User, StringComparison.OrdinalIgnoreCase));
    }

    [LiveFact]
    public async Task Groups_Crud_Members_Libraries()
    {
        var admin = f.Session.Admin.Groups;
        var group = await admin.CreateAsync(TempName("Grp"));
        try
        {
            Assert.Equal(LiveServer.User, group.Owner, ignoreCase: true);
            Assert.False(group.IsDepartment);

            var page = await admin.ListAsync(1, 1);
            Assert.Single(page.Items);
            Assert.True(page.HasNextPage);

            Assert.Contains(await admin.SearchAsync(group.Name), g => g.Id == group.Id);

            var renamed = await admin.RenameAsync(group.Id, group.Name + "r");
            Assert.Equal(group.Name + "r", renamed.Name);

            var members = await admin.ListMembersAsync(group.Id);
            Assert.Equal(1, members.TotalCount);

            var other = await OtherUserAsync();
            if (other != null)
            {
                var added = await admin.AddMembersAsync(group.Id, [other, "nobody-SeafClient@example.invalid"]);
                Assert.Contains(added.Success, m => m.Email == other);
                Assert.Single(added.Failed);

                var promoted = await admin.SetAdminAsync(group.Id, other, true);
                Assert.True(promoted.IsAdmin);
                Assert.Equal(2, (await admin.ListMembersAsync(group.Id)).TotalCount);

                await admin.RemoveMemberAsync(group.Id, other);
                Assert.Equal(1, (await admin.ListMembersAsync(group.Id)).TotalCount);
            }

            // Share the fixture library with the group, list it, then remove it.
            await f.Session.Admin.Shares.ShareWithGroupsAsync(f.LibraryId, [group.Id], SeafSharePermission.R);
            var libs = await admin.ListLibrariesAsync(group.Id);
            var shared = Assert.Single(libs);
            Assert.Equal(f.LibraryId, shared.LibraryId);
            Assert.Equal("r", shared.Permission);
            await admin.RemoveLibraryAsync(group.Id, f.LibraryId);
            Assert.Empty(await admin.ListLibrariesAsync(group.Id));
        }
        finally
        {
            await admin.DeleteAsync(group.Id);
        }
        Assert.DoesNotContain(await admin.SearchAsync(group.Name), g => g.Id == group.Id);
    }

    [LiveFact]
    public async Task Libraries_Crud_History_Folder_Trash()
    {
        var admin = f.Session.Admin.Libraries;
        var lib = await admin.CreateAsync(TempName("Lib"));
        var deleted = false;
        try
        {
            Assert.Equal(LiveServer.User, lib.Owner, ignoreCase: true);
            Assert.Equal(lib.Name, (await admin.GetAsync(lib.Id)).Name);

            var page = await admin.ListAsync(1, 1);
            Assert.Single(page.Items);
            Assert.True(page.HasNextPage);

            Assert.Contains(await admin.SearchAsync(lib.Name), l => l.Id == lib.Id);
            Assert.Contains(await admin.FindAsync(name: lib.Name), l => l.Id == lib.Id);
            Assert.Contains(await admin.FindAsync(ownerEmail: LiveServer.User), l => l.Id == lib.Id);

            var renamed = await admin.RenameAsync(lib.Id, lib.Name + "r");
            Assert.Equal(lib.Name + "r", renamed.Name);

            Assert.Equal("read-only", (await admin.SetStatusAsync(lib.Id, SeafLibraryStatus.ReadOnly)).Status);
            Assert.Equal("normal", (await admin.SetStatusAsync(lib.Id, SeafLibraryStatus.Normal)).Status);

            Assert.Equal(30, await admin.SetHistoryLimitAsync(lib.Id, 30));
            Assert.Equal(30, await admin.GetHistoryLimitAsync(lib.Id));

            // Needs ENABLE_SYS_ADMIN_VIEW_REPO on the server; otherwise 403 "Feature disabled.".
            try
            {
                var folder = await admin.CreateFolderAsync(lib.Id, "/", "adminfolder");
                Assert.False(folder.IsFile);
                Assert.True(await f.Session.Directories.ExistsAsync(lib.Id, "/adminfolder"));
            }
            catch (SeafException e) when (e.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Assert.Contains("disabled", e.ServerMessage, StringComparison.OrdinalIgnoreCase);
            }

            // Delete -> trash -> restore -> delete -> purge (only this library).
            await admin.DeleteAsync(lib.Id);
            deleted = true;
            var trash = await admin.ListTrashAsync(ownerEmail: LiveServer.User);
            Assert.Contains(trash.Items, t => t.Id == lib.Id && t.Deleted != null);

            await admin.RestoreFromTrashAsync(lib.Id);
            deleted = false;
            Assert.Equal(lib.Name + "r", (await admin.GetAsync(lib.Id)).Name);

            await admin.DeleteAsync(lib.Id);
            deleted = true;
            await admin.PurgeFromTrashAsync(lib.Id);
            Assert.DoesNotContain((await admin.ListTrashAsync(ownerEmail: LiveServer.User)).Items, t => t.Id == lib.Id);

            var trashPage = await admin.ListTrashAsync(1, 1);
            Assert.True(trashPage.Items.Count <= 1);
        }
        finally
        {
            if (!deleted) await admin.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task Shares_UserAndGroup()
    {
        var shares = f.Session.Admin.Shares;
        var lib = await f.Session.Admin.Libraries.CreateAsync(TempName("Shr"));
        var group = await f.Session.Admin.Groups.CreateAsync(TempName("ShrGrp"));
        try
        {
            await f.Session.Directories.CreateAsync(lib.Id, "/sub");

            var result = await shares.ShareWithGroupsAsync(lib.Id, [group.Id], SeafSharePermission.R);
            var share = Assert.Single(result.Success);
            Assert.Equal(group.Id, share.GroupId);
            Assert.Empty(result.Failed);

            var updated = await shares.UpdateGroupPermissionAsync(lib.Id, group.Id, SeafSharePermission.Rw);
            Assert.Equal("rw", updated.Permission);

            // Folder share.
            await shares.ShareWithGroupsAsync(lib.Id, [group.Id], SeafSharePermission.R, "/sub");
            Assert.Contains(await shares.ListAsync(lib.Id, "/sub"), s => s.GroupId == group.Id && s.Path == "/sub");
            await shares.UnshareFromGroupAsync(lib.Id, group.Id, "/sub");

            var other = await OtherUserAsync();
            if (other != null)
            {
                var userResult = await shares.ShareWithUsersAsync(lib.Id, [other], SeafSharePermission.R);
                Assert.Contains(userResult.Success, s => s.UserEmail == other);
                Assert.Equal("rw", (await shares.UpdateUserPermissionAsync(lib.Id, other, SeafSharePermission.Rw)).Permission);

                var list = await shares.ListAsync(lib.Id);
                Assert.Contains(list, s => s.ShareType == "user" && s.UserEmail == other);
                Assert.Contains(list, s => s.ShareType == "group" && s.GroupId == group.Id);

                var sharedRepos = await f.Session.Admin.Users.ListSharedLibrariesAsync(LiveServer.User!);
                Assert.Contains(sharedRepos, r => r.LibraryId == lib.Id);

                await shares.UnshareFromUserAsync(lib.Id, other);
            }

            await shares.UnshareFromGroupAsync(lib.Id, group.Id);
            Assert.Empty(await shares.ListAsync(lib.Id));

            var bad = await shares.ShareWithUsersAsync(lib.Id, ["nobody-SeafClient@example.invalid"]);
            Assert.Single(bad.Failed);
        }
        finally
        {
            await f.Session.Admin.Groups.DeleteAsync(group.Id);
            await f.Session.Admin.Libraries.DeleteAsync(lib.Id);
            await f.Session.Admin.Libraries.PurgeFromTrashAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task UserSharedFolders_List()
    {
        // Read-only; the account may or may not have shared folders.
        var folders = await f.Session.Admin.Users.ListSharedFoldersAsync(LiveServer.User!);
        Assert.All(folders, x => Assert.False(string.IsNullOrEmpty(x.LibraryId)));
    }

    [LiveFact]
    public async Task Departments_CreateNestedGetDelete()
    {
        var depts = f.Session.Admin.Departments;
        var root = await depts.CreateAsync(TempName("Dept"));
        try
        {
            Assert.Equal(-1, root.ParentGroupId);
            Assert.Contains(await depts.ListAsync(), d => d.Id == root.Id);

            var child = await depts.CreateAsync(TempName("SubDept"), parentId: root.Id);
            Assert.Equal(root.Id, child.ParentGroupId);

            var detail = await depts.GetAsync(root.Id);
            Assert.Contains(detail.Groups, g => g.Id == child.Id);

            var childDetail = await depts.GetAsync(child.Id, includeAncestors: true);
            Assert.Contains(childDetail.AncestorGroups, g => g.Id == root.Id);

            // Departments are groups: they show up in the admin group list too.
            Assert.Contains(await f.Session.Admin.Groups.SearchAsync(child.Name), g => g.Id == child.Id && g.IsDepartment);

            await depts.DeleteAsync(child.Id);
        }
        finally
        {
            await depts.DeleteAsync(root.Id);
        }
        Assert.DoesNotContain(await depts.ListAsync(), d => d.Id == root.Id);
    }

    [LiveFact]
    public async Task Logs_PaginatedAndDateRange()
    {
        var logs = f.Session.Admin.Logs;

        var logins = await logs.ListLoginsAsync(1, 2);
        Assert.NotEmpty(logins.Items);
        Assert.True(logins.Items.Count <= 2);
        Assert.NotNull(logins.TotalCount);
        Assert.All(logins.Items, l => Assert.NotNull(l.LoginTime));

        var mine = await logs.ListLoginsAsync(1, 5, [LiveServer.User!]);
        Assert.All(mine.Items, l => Assert.Equal(LiveServer.User, l.Email, ignoreCase: true));

        Assert.True((await logs.ListFileAccessAsync(1, 2)).Items.Count <= 2);
        Assert.True((await logs.ListFileUpdatesAsync(1, 2, libraryIds: [f.LibraryId])).Items.All(x => x.LibraryId == f.LibraryId));
        Assert.True((await logs.ListSharePermissionsAsync(1, 2)).Items.Count <= 2);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-30);
        var end = today.AddDays(1);
        Assert.NotEmpty(await logs.GetLoginsAsync(start, end));
        await logs.GetFileAccessAsync(start, end);
        await logs.GetFileUpdatesAsync(start, end);
        await logs.GetPermissionChangesAsync(start, end);
    }

    [LiveFact]
    public async Task ShareLinks_ListGetDirentsDelete()
    {
        await f.Session.Directories.EnsureAsync(f.LibraryId, "/adminlinks/inner");
        var link = await f.Session.ShareLinks.CreateAsync(f.LibraryId, "/adminlinks");
        try
        {
            var admin = f.Session.Admin.ShareLinks;
            var page = await admin.ListAsync(1, 100, orderBy: "ctime");
            Assert.True(page.TotalCount >= 1);

            var detail = await admin.GetAsync(link.Token);
            Assert.True(detail.IsDir);
            Assert.Equal(f.LibraryId, detail.LibraryId);
            Assert.Equal(LiveServer.User, detail.CreatorEmail, ignoreCase: true);

            var dirents = await admin.ListDirentsAsync(link.Token);
            Assert.Contains(dirents, d => d.ObjName == "inner" && d.IsDir);

            await admin.DeleteAsync(link.Token);
            var ex = await Assert.ThrowsAsync<SeafException>(() => admin.GetAsync(link.Token));
            Assert.True(ex.IsNotFound);
        }
        finally
        {
            try { await f.Session.ShareLinks.DeleteAsync(link.Token); } catch (SeafException) { }
        }
    }

    [LiveFact]
    public async Task Notifications_List()
    {
        var page = await f.Session.Admin.Notifications.ListAsync(1, 2);
        Assert.True(page.Items.Count <= 2);
        Assert.True(page.TotalCount >= page.Items.Count);
        Assert.All(page.Items, n => Assert.False(string.IsNullOrEmpty(n.Type)));
    }

    [LiveFact]
    public async Task Organizations_FeatureDisabled_Throws()
    {
        // The test server does not enable MULTI_TENANCY.
        var ex = await Assert.ThrowsAsync<SeafException>(() => f.Session.Admin.Organizations.ListAsync());
        Assert.Contains("not enabled", ex.ServerMessage, StringComparison.OrdinalIgnoreCase);
        ex = await Assert.ThrowsAsync<SeafException>(() => f.Session.Admin.Organizations.SearchAsync("a"));
        Assert.Contains("not enabled", ex.ServerMessage, StringComparison.OrdinalIgnoreCase);
    }
}
