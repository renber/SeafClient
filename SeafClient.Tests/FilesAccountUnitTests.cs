using System.Net;

namespace SeafClient.Tests;

public class FilesAccountUnitTests
{
    private sealed class Recorder(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        public Recorder(string body) : this(_ => body) { }

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        public List<string?> ContentTypes { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            ContentTypes.Add(request.Content?.Headers.ContentType?.ToString());
            Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(request)) };
        }
    }

    private static SeafSession Session(Recorder r) => SeafSession.FromToken(new Uri("http://seaf.test/"), "tok",
        new SeafClientOptions { HttpClient = new HttpClient(r), MaxRetries = 0 });

    [Fact]
    public async Task SearchFiles_BuildsQuery()
    {
        var r = new Recorder("""{"total":1,"has_more":false,"results":[{"repo_id":"r","name":"a.txt","fullpath":"/d/a.txt","is_dir":false,"size":3,"last_modified":1520836447}]}""");
        using var s = Session(r);
        var result = await s.Search.SearchFilesAsync(new SeafSearchQuery
        {
            Query = "tax return",
            LibraryId = "lib",
            Path = "d/",
            FileTypes = ["Document", "PDF"],
            Extensions = ["md", "txt"],
            SizeFrom = 10,
            ModifiedFrom = DateTimeOffset.FromUnixTimeSeconds(100),
            EntryType = SeafEntryType.File,
            FileNamesOnly = true,
        });

        var q = r.Requests[0].RequestUri!.Query;
        Assert.Contains("q=tax%20return", q);
        Assert.Contains("search_repo=lib", q);
        Assert.Contains("search_path=%2Fd", q);
        Assert.Contains("search_ftypes=custom", q);
        Assert.Contains("ftype=Document&ftype=PDF", q);
        Assert.Contains("input_fexts=md%2Ctxt", q);
        Assert.Contains("size_from=10", q);
        Assert.Contains("time_from=100", q);
        Assert.Contains("obj_type=file", q);
        Assert.Contains("search_filename_only=true", q);

        var hit = Assert.Single(result.Results);
        Assert.Equal("/d/a.txt", hit.Path);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1520836447), hit.Modified);
    }

    [Fact]
    public async Task SearchFiles_DefaultScope_HasNoCustomTypes()
    {
        var r = new Recorder("""{"total":0,"has_more":false,"results":[]}""");
        using var s = Session(r);
        await s.Search.SearchFilesAsync(new SeafSearchQuery { Query = "x", Scope = SeafSearchScope.Shared });
        var q = r.Requests[0].RequestUri!.Query;
        Assert.Contains("search_repo=shared", q);
        Assert.DoesNotContain("search_ftypes", q);
        Assert.DoesNotContain("search_filename_only", q);
    }

    [Fact]
    public async Task Zip_RepeatsDirentsAndBuildsDownloadUri()
    {
        var r = new Recorder("""{"zip_token":"tok-1"}""");
        using var s = Session(r);
        var task = await s.Items.CreateZipAsync("lib", "/a b", ["x", "y z"]);
        Assert.Equal("?parent_dir=%2Fa%20b&dirents=x&dirents=y%20z", r.Requests[0].RequestUri!.Query);
        Assert.Equal("http://seaf.test/seafhttp/zip/tok-1", task.DownloadUri.AbsoluteUri);
    }

    [Fact]
    public async Task AsyncTask_EmptyId_IsCompleted_AndWaitDoesNotPoll()
    {
        var r = new Recorder("""{"task_id":""}""");
        using var s = Session(r);
        var task = await s.Items.StartCopyAsync("a", "/", ["f"], "a", "/d");
        Assert.True(task.IsCompleted);
        Assert.True((await s.Items.WaitAsync(task)).Successful);
        Assert.Single(r.Requests);
        Assert.Contains("\"src_dirents\":[\"f\"]", r.Bodies[0]);
    }

    [Fact]
    public async Task GroupTransfer_SendsOwner()
    {
        var r = new Recorder("""{"id":7,"name":"g","owner":"new@x.y","admins":["new@x.y"]}""");
        using var s = Session(r);
        var g = await s.Groups.TransferAsync(7, "new@x.y");
        Assert.Equal(HttpMethod.Put, r.Requests[0].Method);
        Assert.Equal("/api/v2.1/groups/7/", r.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("owner=new%40x.y", r.Bodies[0]);
        Assert.Equal("new@x.y", g.Owner);
    }

    [Fact]
    public async Task InvitationsBatch_RepeatsAccepter()
    {
        var r = new Recorder("""{"success":[],"failed":[{"email":"b@x.y","error_msg":"already invited"}]}""");
        using var s = Session(r);
        var result = await s.Invitations.CreateManyAsync(["a@x.y", "b@x.y"]);
        Assert.Equal("type=guest&accepter=a%40x.y&accepter=b%40x.y", r.Bodies[0]);
        Assert.Equal("already invited", Assert.Single(result.Failed).ErrorMsg);
    }

    [Fact]
    public async Task Upload_UsesUnquotedMultipartBoundary()
    {
        var r = new Recorder(req => req.Method == HttpMethod.Get
            ? "\"http://seaf.test/seafhttp/upload-api/tok\""
            : """[{"name":"hello.txt","id":"abc","size":5}]""");
        using var s = Session(r);

        await s.Files.UploadAsync("lib", "/dir", "hello.txt", new MemoryStream("hello"u8.ToArray()));

        Assert.Equal(2, r.Requests.Count);
        AssertUnquotedMultipart(r.ContentTypes[1], r.Bodies[1]);
    }

    [Fact]
    public async Task UploadAvatar_UsesUnquotedMultipartBoundary()
    {
        var r = new Recorder("""{"avatar_url":"http://seaf.test/media/avatars/pic.png"}""");
        using var s = Session(r);

        await s.Account.UploadAvatarAsync(new MemoryStream([1, 2, 3]), "pic.png");

        Assert.Single(r.Requests);
        AssertUnquotedMultipart(r.ContentTypes[0], r.Bodies[0]);
    }

    private sealed class SequenceHandler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => respond(request, Calls++);
    }

    [Fact]
    public async Task UploadAvatar_RetriesOn429_DoesNotDisposeCallerStream()
    {
        byte[]? uploadedBytes = null;
        var handler = new SequenceHandler(async (req, attempt) =>
        {
            if (attempt == 0)
            {
                if (req.Content != null) await req.Content.ReadAsByteArrayAsync();
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }

            uploadedBytes = req.Content == null ? null : await req.Content.ReadAsByteArrayAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"avatar_url":"http://seaf.test/media/avatars/pic.png"}""")
            };
        });

        using var s = SeafSession.FromToken(new Uri("http://seaf.test/"), "tok",
            new SeafClientOptions { HttpClient = new HttpClient(handler), MaxRetries = 2, RetryBaseDelay = TimeSpan.FromMilliseconds(1) });
        using var ms = new MemoryStream([10, 20, 30, 40]);

        var url = await s.Account.UploadAvatarAsync(ms, "a\"b\\c.png");

        Assert.Equal("http://seaf.test/media/avatars/pic.png", url);
        Assert.Equal(2, handler.Calls);
        Assert.True(ms.CanRead);
        Assert.Equal(4, ms.Position);
        Assert.NotNull(uploadedBytes);
    }

    private static void AssertUnquotedMultipart(string? contentType, string body)
    {
        Assert.NotNull(contentType);
        const string prefix = "multipart/form-data; boundary=";
        Assert.StartsWith(prefix + "----SeafClient", contentType);
        Assert.DoesNotContain("\"", contentType);

        var boundary = contentType[prefix.Length..];
        Assert.StartsWith("--" + boundary + "\r\n", body);
        Assert.EndsWith("--" + boundary + "--\r\n", body);
    }
}
