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

    [Fact]
    public async Task Files_ReadAllTextAsync_IgnoresServerCharset_AndDecodesUtf8()
    {
        var handler = new SequenceHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/file/"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"http://seaf.test/dl/1\"") });

            var res = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("héllo 世界"u8.ToArray()) };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") { CharSet = "gbk" };
            return Task.FromResult(res);
        });

        using var s = SeafSession.FromToken(new Uri("http://seaf.test/"), "tok", new SeafClientOptions { HttpClient = new HttpClient(handler) });
        Assert.Equal("héllo 世界", await s.Files.ReadAllTextAsync("repo-1", "/hello.txt"));
    }

    [Fact]
    public async Task Files_ReadAllBytesAsync_ReturnsExactBytes()
    {
        byte[] expected = [0x01, 0xFE, 0x00, 0x7F, 0x80];
        var handler = new SequenceHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/file/"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"http://seaf.test/dl/2\"") });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(expected) });
        });

        using var s = SeafSession.FromToken(new Uri("http://seaf.test/"), "tok", new SeafClientOptions { HttpClient = new HttpClient(handler) });
        Assert.Equal(expected, await s.Files.ReadAllBytesAsync("repo-1", "/bin.dat"));
    }

    [Fact]
    public async Task Files_ReadAllBytesAndText_HandleEmptyAndUnknownContentLength()
    {
        byte[] binaryPayload = [0x10, 0x20, 0x30, 0x40, 0x50];
        const string textPayload = "café ☕";

        var handler = new SequenceHandler((req, _) =>
        {
            var p = req.RequestUri!.AbsolutePath;
            if (p.EndsWith("/file/"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"\"http://seaf.test/dl/{req.RequestUri.Query}\"") });
            if (req.RequestUri.Query.Contains("empty"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });
            if (req.RequestUri.Query.Contains("chunked-bin"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(binaryPayload) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(System.Text.Encoding.UTF8.GetBytes(textPayload)) });
        });

        using var s = SeafSession.FromToken(new Uri("http://seaf.test/"), "tok", new SeafClientOptions { HttpClient = new HttpClient(handler) });

        Assert.Empty(await s.Files.ReadAllBytesAsync("repo-1", "/empty"));
        Assert.Equal(string.Empty, await s.Files.ReadAllTextAsync("repo-1", "/empty"));
        Assert.Equal(binaryPayload, await s.Files.ReadAllBytesAsync("repo-1", "/chunked-bin"));
        Assert.Equal(textPayload, await s.Files.ReadAllTextAsync("repo-1", "/chunked-text"));
    }

    private sealed class UnknownLengthContent(byte[] data) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => stream.WriteAsync(data, 0, data.Length);
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(data, writable: false));
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    [Fact]
    public async Task ProgressStreamContent_ReportsMultiChunkProgress_AndLeavesSourceOpen()
    {
        var reported = new List<long>();
        var progress = new SyncProgress(reported.Add);
        using var source = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        using var content = new SeafClient.Api.ProgressStreamContent(source, progress, bufferSize: 4);
        using var dest = new MemoryStream();

        await content.CopyToAsync(dest);

        Assert.Equal([4L, 8L, 10L], reported);
        Assert.Equal(source.ToArray(), dest.ToArray());
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task ReadStreamAsync_DisposesResponse_WhenReadAsStreamThrows()
    {
        var content = new ThrowingStreamHttpContent();
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SeafClient.Internal.SeafTransport.ReadStreamAsync(response, CancellationToken.None));
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task Upload_ValidatesFileNameBackslashAndNullContent()
    {
        using var s = SeafSession.FromToken(new Uri("http://seaf.test/"), "tok", new SeafClientOptions { HttpClient = new HttpClient(new SequenceHandler((_, _) => throw new InvalidOperationException())) });
        using var ms = new MemoryStream([1]);

        await Assert.ThrowsAsync<ArgumentException>(() => s.Files.UploadAsync("r", "/", @"sub\file.txt", ms));
        await Assert.ThrowsAsync<ArgumentNullException>(() => s.Files.UploadAsync("r", "/", "ok.txt", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => s.Files.UpdateAsync("r", "/ok.txt", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => s.UploadLinks.UploadAsync("tok", @"sub\file.txt", ms));
        await Assert.ThrowsAsync<ArgumentNullException>(() => s.UploadLinks.UploadAsync("tok", "ok.txt", null!));

        using var repo = SeafRepoTokenSession.FromToken(new Uri("http://seaf.test/"), "tok", new SeafClientOptions { HttpClient = new HttpClient(new SequenceHandler((_, _) => throw new InvalidOperationException())) });
        await Assert.ThrowsAsync<ArgumentException>(() => repo.Files.UploadAsync("/", @"sub\file.txt", ms));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.Files.UploadAsync("/", "ok.txt", null!));
    }

    private sealed class SyncProgress(Action<long> onReport) : IProgress<long>
    {
        public void Report(long value) => onReport(value);
    }

    private sealed class ThrowingStreamHttpContent : HttpContent
    {
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => Task.CompletedTask;
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromException<Stream>(new InvalidOperationException("boom"));
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
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
