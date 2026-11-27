using System.Net;
using System.Text;

namespace SeafClient.Tests;

/// <summary>
/// Live tests for SDK methods that the area-specific test classes do not exercise.
/// Not covered on purpose: <c>Admin.Libraries.EmptyTrashAsync</c> (permanently purges the whole server's library trash).
/// </summary>
[Collection(LiveCollection.Name)]
public class CoverageGapTests(LiveFixture f)
{
    private SeafSession S => f.Session;

    [LiveFact]
    public async Task Session_GetTokenAsync_ReturnsUsableToken()
    {
        var token = await SeafSession.GetTokenAsync(LiveKit.ServerUri, LiveServer.User!, LiveServer.Password!);
        Assert.Equal(40, token.Length);
        using var s = SeafSession.FromToken(LiveKit.ServerUri, token);
        Assert.True(await s.Server.AuthPingAsync());
    }

    [LiveFact]
    public async Task Libraries_DefaultLibrary()
    {
        var id = await S.Libraries.GetDefaultIdAsync();
        Assert.False(string.IsNullOrEmpty(id));
        // The account already has a default library, so "ensure" returns it without creating another.
        Assert.Equal(id, await S.Libraries.EnsureDefaultIdAsync());
        Assert.Equal(id, (await S.Libraries.GetAsync(id!)).Id);
    }

    [LiveFact]
    public async Task Libraries_EncryptedLibrary_Decrypt()
    {
        const string password = "SeafClient-pw-1";
        var lib = await LiveKit.TempLibrary(S, "SeafClientEnc", password);
        try
        {
            Assert.True(lib.Encrypted);
            var wrong = await Assert.ThrowsAsync<SeafException>(() => S.Libraries.DecryptAsync(lib.Id, "not-the-password"));
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

            await S.Libraries.DecryptAsync(lib.Id, password);
            await LiveKit.Upload(S, lib.Id, "/", "secret.txt", "classified");
            Assert.Equal("classified", await S.Files.ReadAllTextAsync(lib.Id, "/secret.txt"));
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task Libraries_ShareWithUser_Single()
    {
        var other = (await LiveKit.OtherUsersAsync(S)).FirstOrDefault();
        Assert.NotNull(other);
        var lib = await LiveKit.TempLibrary(S);
        try
        {
            await S.Libraries.ShareWithUserAsync(lib.Id, other!, SeafSharePermission.R);
            var share = Assert.Single(await S.Libraries.ListSharesAsync(lib.Id), x => x.ShareType == "user");
            Assert.Equal(other, share.UserInfo?.Name);
            Assert.Equal("r", share.Permission);

            await S.Libraries.UnshareFromUserAsync(lib.Id, other!);
            Assert.DoesNotContain(await S.Libraries.ListSharesAsync(lib.Id), x => x.ShareType == "user");
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task Groups_AddMember_Single()
    {
        var other = (await LiveKit.OtherUsersAsync(S)).FirstOrDefault();
        Assert.NotNull(other);
        var group = await S.Groups.CreateAsync(LiveKit.Unique("SeafClientGrp", 24));
        try
        {
            var member = await S.Groups.AddMemberAsync(group.Id, other!);
            Assert.Equal(other, member.Email);
            Assert.False(member.IsAdmin);
            Assert.Contains(await S.Groups.ListMembersAsync(group.Id), m => m.Email == other);

            var dup = await Assert.ThrowsAsync<SeafException>(() => S.Groups.AddMemberAsync(group.Id, other!));
            Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        }
        finally
        {
            await S.Groups.DeleteAsync(group.Id);
        }
    }

    [LiveFact]
    public async Task Files_LocalUpload_DownloadVariants_Links()
    {
        var dir = "/gap-files";
        var local = Path.Combine(Path.GetTempPath(), LiveKit.Unique("seafclient", 20) + ".txt");
        await File.WriteAllTextAsync(local, "from disk ✓");
        try
        {
            var uploaded = await S.Files.UploadFileAsync(f.LibraryId, dir, local, new SeafUploadOptions { CreateParents = true });
            Assert.Equal(Path.GetFileName(local), uploaded.Name);
            var expected = Encoding.UTF8.GetBytes("from disk ✓");

            Assert.Equal(expected, await S.Files.ReadAllBytesAsync(f.LibraryId, uploaded.Path));

            using var ms = new MemoryStream();
            await S.Files.DownloadAsync(f.LibraryId, uploaded.Path, ms);
            Assert.Equal(expected, ms.ToArray());

            var uploadLink = await S.Files.GetUploadLinkAsync(f.LibraryId, dir);
            Assert.Contains("/upload-api/", uploadLink);
            var updateLink = await S.Files.GetUpdateLinkAsync(f.LibraryId, dir);
            Assert.Contains("/update-api/", updateLink);
        }
        finally
        {
            File.Delete(local);
            await S.Directories.DeleteIfExistsAsync(f.LibraryId, dir);
        }
    }

    [LiveFact]
    public async Task Files_RevisionDownloads()
    {
        var lib = await LiveKit.TempLibrary(S);
        try
        {
            await LiveKit.Upload(S, lib.Id, "/", "r.txt", "first");
            var firstCommit = (await S.Libraries.GetHistoryAsync(lib.Id, 1, 1)).Items[0].CommitId;
            await S.Files.UpdateAsync(lib.Id, "/r.txt", new MemoryStream("second"u8.ToArray()));
            Assert.Equal("second", await S.Files.ReadAllTextAsync(lib.Id, "/r.txt"));

            var link = await S.Files.GetRevisionDownloadLinkAsync(lib.Id, "/r.txt", firstCommit);
            Assert.StartsWith("http", link);

            await using (var stream = await S.Files.OpenRevisionReadAsync(lib.Id, "/r.txt", firstCommit))
            using (var reader = new StreamReader(stream))
                Assert.Equal("first", await reader.ReadToEndAsync());

            Assert.Equal("first"u8.ToArray(), await S.Files.ReadRevisionBytesAsync(lib.Id, "/r.txt", firstCommit));
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task FileComments_ByUuid()
    {
        var dir = "/gap-comments";
        await LiveKit.Upload(S, f.LibraryId, dir, "c.txt", "comment me");
        try
        {
            var uuid = await S.FileComments.GetFileUuidAsync(f.LibraryId, dir + "/c.txt");
            Assert.False(string.IsNullOrEmpty(uuid));

            var created = await S.FileComments.CreateByUuidAsync(f.LibraryId, uuid, "by uuid");
            Assert.Equal("by uuid", created.Comment);
            Assert.Contains(await S.FileComments.ListByUuidAsync(f.LibraryId, uuid), c => c.Id == created.Id);

            var resolved = await S.FileComments.UpdateByUuidAsync(f.LibraryId, uuid, created.Id, resolved: true);
            Assert.True(resolved.Resolved);
            Assert.Contains(await S.FileComments.ListByUuidAsync(f.LibraryId, uuid, resolved: true), c => c.Id == created.Id);
            Assert.DoesNotContain(await S.FileComments.ListByUuidAsync(f.LibraryId, uuid, resolved: false), c => c.Id == created.Id);

            await S.FileComments.DeleteByUuidAsync(f.LibraryId, uuid, created.Id);
            Assert.Empty(await S.FileComments.ListByUuidAsync(f.LibraryId, uuid));
        }
        finally
        {
            await S.Directories.DeleteIfExistsAsync(f.LibraryId, dir);
        }
    }

    [LiveFact]
    public async Task Account_ClientLoginToken()
    {
        var token = await S.Account.GetClientLoginTokenAsync();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.NotEqual(token, await S.Account.GetClientLoginTokenAsync());
    }

    [LiveFact]
    public async Task Account_UnlinkDevice_UnknownDevice()
    {
        // No real device may be unlinked on the test server. The server validates platform and device_id (400 when missing)
        // before unlinking, and answers 500 for a device that does not exist, which proves both fields arrive.
        Assert.Empty(await S.Account.ListDevicesAsync());
        var noPlatform = await Assert.ThrowsAsync<SeafException>(() => S.Account.UnlinkDeviceAsync("", "x"));
        Assert.Equal(HttpStatusCode.BadRequest, noPlatform.StatusCode);
        Assert.Contains("platform", noPlatform.ServerMessage);
        var noDevice = await Assert.ThrowsAsync<SeafException>(() => S.Account.UnlinkDeviceAsync("linux", ""));
        Assert.Contains("device_id", noDevice.ServerMessage);
        var unknown = await Assert.ThrowsAsync<SeafException>(() => S.Account.UnlinkDeviceAsync("linux", "seafclient-no-such-device"));
        Assert.Equal(HttpStatusCode.InternalServerError, unknown.StatusCode);
    }

    [LiveFact]
    public async Task Invitations_Revoke_ForbiddenForRole()
    {
        var ex = await Assert.ThrowsAsync<SeafException>(() => S.Invitations.RevokeAsync("seafclientnosuchtoken"));
        Assert.True(ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, ex.ToString());

        // Batch invitations are blocked the same way for this role (the request shape is covered by a unit test).
        var batch = await Assert.ThrowsAsync<SeafException>(() => S.Invitations.CreateManyAsync(
            Enumerable.Range(0, 5).Select(i => $"guest{i}-seafclient@example.invalid")));
        Assert.Equal(HttpStatusCode.Forbidden, batch.StatusCode);
    }

    [LiveFact]
    public async Task Admin_DepartmentQuota_AndMove()
    {
        var parent = await S.Admin.Departments.CreateAsync(LiveKit.Unique("SeafClientDeptA", 26));
        var child = await S.Admin.Departments.CreateAsync(LiveKit.Unique("SeafClientDeptB", 26));
        try
        {
            const long hundredMb = 100L * 1000 * 1000;
            var withQuota = await S.Admin.Groups.SetQuotaAsync(parent.Id, hundredMb);
            Assert.Equal(hundredMb, withQuota.Quota);
            Assert.Equal(hundredMb, (await S.Admin.Departments.GetAsync(parent.Id)).Quota);
            Assert.Equal(-2, (await S.Admin.Groups.SetQuotaAsync(parent.Id, -2)).Quota);

            await S.Admin.Groups.MoveDepartmentAsync(child.Id, parent.Id);
            Assert.Equal(parent.Id, (await S.Admin.Departments.GetAsync(child.Id)).ParentGroupId);
        }
        finally
        {
            await S.Admin.Departments.DeleteAsync(child.Id);
            await S.Admin.Departments.DeleteAsync(parent.Id);
        }
    }

    [LiveFact]
    public async Task Metadata_FaceRecognition_RemovedInSeafile14()
    {
        var lib = await LiveKit.TempLibrary(S, "SeafClientFace");
        try
        {
            await S.Metadata.SetEnabledAsync(lib.Id, true);
            var calls = new Func<Task>[]
            {
                () => S.Metadata.Faces.DisableAsync(lib.Id),
                () => S.Metadata.Faces.RenamePersonAsync(lib.Id, "p1", "Someone"),
                () => S.Metadata.Faces.ListPhotosAsync(lib.Id, "p1"),
                () => S.Metadata.Faces.AddPhotosAsync(lib.Id, ["p1"], ["r1"]),
                () => S.Metadata.Faces.RemovePhotosAsync(lib.Id, "p1", ["r1"]),
                () => S.Metadata.Faces.SetCoverPhotoAsync(lib.Id, "p1", "r1"),
            };
            var version = (await S.Server.GetInfoAsync()).ParsedVersion;
            foreach (var call in calls)
            {
                var ex = await Assert.ThrowsAsync<SeafException>(call);
                if (version?.Major >= 14) Assert.True(ex.IsNotFound, ex.ToString());
            }
        }
        finally
        {
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }

    [LiveFact]
    public async Task RepoToken_LocalUpload_Downloads()
    {
        var lib = await LiveKit.TempLibrary(S, "SeafClientRTgap");
        var local = Path.Combine(Path.GetTempPath(), LiveKit.Unique("seafclient", 20) + ".txt");
        await File.WriteAllTextAsync(local, "repo token upload");
        try
        {
            var token = await S.Libraries.CreateApiTokenAsync(lib.Id, "seafclient-gap", SeafRepoTokenPermission.Rw);
            using var repo = SeafRepoTokenSession.FromToken(LiveKit.ServerUri, token.ApiToken);
            Assert.Equal(lib.Id, (await repo.GetInfoAsync()).Id);

            Assert.Contains("/upload-api/", await repo.Files.GetUploadLinkAsync("/"));
            var uploaded = await repo.Files.UploadFileAsync("/in", local, new SeafUploadOptions { CreateParents = true });
            Assert.Equal("/in/" + Path.GetFileName(local), uploaded.Path);

            Assert.Equal("repo token upload"u8.ToArray(), await repo.Files.ReadAllBytesAsync(uploaded.Path));
            using var ms = new MemoryStream();
            await repo.Files.DownloadAsync(uploaded.Path, ms);
            Assert.Equal("repo token upload"u8.ToArray(), ms.ToArray());
        }
        finally
        {
            File.Delete(local);
            foreach (var t in await S.Libraries.ListApiTokensAsync(lib.Id))
                await S.Libraries.DeleteApiTokenAsync(lib.Id, t.AppName);
            await S.Libraries.DeleteAsync(lib.Id);
        }
    }
}
