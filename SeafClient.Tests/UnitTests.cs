using System.Net;
using System.Text.Json;
using SeafClient.Internal;

namespace SeafClient.Tests;

public class SeafPathTests
{
    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("a", "/a")]
    [InlineData("/a/", "/a")]
    [InlineData("//a//b///", "/a/b")]
    [InlineData("a\\b", "/a/b")]
    [InlineData("/a b/c (1).txt", "/a b/c (1).txt")]
    public void Normalize(string? input, string expected) => Assert.Equal(expected, SeafPath.Normalize(input));

    [Fact]
    public void Combine_SkipsEmptySegments() => Assert.Equal("/12/Uploads/2026", SeafPath.Combine("12", "", null, "Uploads/", "/2026"));

    [Theory]
    [InlineData("/a/b/c.txt", "/a/b", "c.txt")]
    [InlineData("/a", "/", "a")]
    [InlineData("/", "/", "")]
    public void ParentAndName(string path, string parent, string name)
    {
        Assert.Equal(parent, SeafPath.GetParent(path));
        Assert.Equal(name, SeafPath.GetName(path));
    }
}

public class JsonTests
{
    [Fact]
    public void DirEntry_UnixTimestamp_AndPath()
    {
        var e = SeafJson.Deserialize<SeafDirEntry>("""{"type":"file","id":"abc","name":"hi.txt","mtime":1794091693,"parent_dir":"/a/","size":15,"starred":false}""");
        Assert.Equal(SeafEntryType.File, e.Type);
        Assert.Equal("/a/hi.txt", e.Path);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1794091693), e.Modified);
        Assert.Equal(15, e.Size);
    }

    [Fact]
    public void Directory_IsoTimestamp_PathNormalized()
    {
        var d = SeafJson.Deserialize<SeafDirectory>("""{"repo_id":"r","path":"/a/","name":"a","mtime":"2026-11-07T22:47:54+00:00","permission":"rw"}""");
        Assert.Equal("/a", d.Path);
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 22, 47, 54, TimeSpan.Zero), d.Modified);
    }

    [Fact]
    public void User_IntegerBooleans()
    {
        var u = SeafJson.Deserialize<SeafUser>("""{"email":"x@auth.local","name":"X","is_staff":1,"is_active":0,"quota_total":-2,"create_time":"2026-11-07T21:21:23+00:00","last_login":""}""");
        Assert.True(u.IsStaff);
        Assert.False(u.IsActive);
        Assert.Null(u.LastLogin);
        Assert.NotNull(u.Created);
    }

    [Fact]
    public void ShareLink_NullExpiry()
    {
        var l = SeafJson.Deserialize<SeafShareLink>("""{"token":"t","link":"http://h/d/t/","repo_id":"r","path":"/a/","is_dir":true,"expire_date":null,"is_expired":false,"permissions":{"can_edit":false,"can_download":true,"can_upload":true}}""");
        Assert.Null(l.ExpiresAt);
        Assert.False(l.IsExpiredNow);
        Assert.True(l.Permissions.CanUpload);
    }

    [Theory]
    [InlineData("""{"error_msg":"Folder /nope not found."}""", "Folder /nope not found.")]
    [InlineData("""{"detail":"Invalid token"}""", "Invalid token")]
    [InlineData("""{"non_field_errors":["Unable to login with provided credentials."]}""", "Unable to login with provided credentials.")]
    [InlineData("""{"error": "Parent dir doesn't exist.\n"}""", "Parent dir doesn't exist.\n")]
    [InlineData("\"plain\"", "plain")]
    [InlineData("<html><head><title>502 Bad Gateway</title></head></html>", "502 Bad Gateway")]
    [InlineData("""{"email":["Enter a valid email address."]}""", "email: Enter a valid email address.")]
    public void ExtractErrorMessage(string body, string expected) => Assert.Equal(expected, SeafTransport.ExtractErrorMessage(body));
}

public class TransportTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request, Calls++));
    }

    private static SeafSession Session(StubHandler handler, int retries = 2) => SeafSession.FromToken(new Uri("http://seaf.test/sub"), "tok",
        new SeafClientOptions { HttpClient = new HttpClient(handler), MaxRetries = retries, RetryBaseDelay = TimeSpan.FromMilliseconds(1) });

    [Fact]
    public async Task RetriesOn429ThenSucceeds()
    {
        var handler = new StubHandler((req, n) =>
        {
            Assert.Equal("Token tok", req.Headers.Authorization?.ToString());
            Assert.Equal("http://seaf.test/sub/api2/ping/", req.RequestUri!.AbsoluteUri);
            return n < 2 ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"pong\"") };
        });
        Assert.True(await Session(handler).Server.PingAsync());
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task RateLimitExhausted_ThrowsTypedException()
    {
        var handler = new StubHandler((_, _) =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("""{"detail":"Request was throttled."}""") };
            r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
            return r;
        });
        var ex = await Assert.ThrowsAsync<SeafRateLimitException>(() => Session(handler, retries: 1).Server.GetInfoAsync());
        Assert.Equal("Request was throttled.", ex.ServerMessage);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task PostIsNotRetriedOn502()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var ex = await Assert.ThrowsAsync<SeafException>(() => Session(handler).Groups.CreateAsync("g"));
        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task NotFound_MapsToTryGetNull()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error_msg":"File /x not found."}""") });
        Assert.Null(await Session(handler).Files.TryGetAsync("lib", "/x"));
    }

    [Fact]
    public async Task QueryValuesAreEscaped()
    {
        Uri? seen = null;
        var handler = new StubHandler((req, _) =>
        {
            seen = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"dirent_list":[]}""") };
        });
        await Session(handler).Directories.ListAsync("lib", "/a b/c&d#e");
        Assert.Equal("?p=%2Fa%20b%2Fc%26d%23e", seen!.Query);
    }

    [Fact]
    public void SeafException_StatusHelpers_Distinguish401And403()
    {
        var unauth = new SeafException(HttpStatusCode.Unauthorized, "Invalid token");
        Assert.True(unauth.IsUnauthorized);
        Assert.False(unauth.IsForbidden);

        var forbidden = new SeafException(HttpStatusCode.Forbidden, "Permission denied");
        Assert.False(forbidden.IsUnauthorized);
        Assert.True(forbidden.IsForbidden);

        Assert.True(new SeafException(HttpStatusCode.BadRequest, "Bad").IsBadRequest);
        Assert.True(new SeafRateLimitException("Slow", null, null, null, null).IsRateLimited);
        Assert.True(new SeafException(HttpStatusCode.BadGateway, "Bad gateway").IsServerError);
    }

    [Fact]
    public async Task AuthPingAsync_ReturnsFalseOn401_ThrowsOn403()
    {
        var h401 = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("""{"detail":"Invalid token"}""") });
        Assert.False(await Session(h401, retries: 0).Server.AuthPingAsync());

        var h403 = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("""{"detail":"Forbidden"}""") });
        var ex = await Assert.ThrowsAsync<SeafException>(() => Session(h403, retries: 0).Server.AuthPingAsync());
        Assert.True(ex.IsForbidden);
    }

    [Fact]
    public async Task ShareLinks_GetOrCreate_DefaultReuses_OptionsEnforceLifetimeAndPermissions()
    {
        var expiresSoon = DateTimeOffset.UtcNow.AddMinutes(5);
        var expiresLater = DateTimeOffset.UtcNow.AddDays(7);
        var calls = new List<(HttpMethod Method, string Path)>();
        var existingJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                token = "t1",
                link = "http://seaf.test/d/t1/",
                repo_id = "lib",
                path = "/a",
                expire_date = expiresSoon,
                is_expired = false,
                password = "",
                permissions = new { can_edit = false, can_download = true, can_upload = true }
            }
        });

        var handler = new StubHandler((req, _) =>
        {
            calls.Add((req.Method, req.RequestUri!.AbsolutePath));
            if (req.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(existingJson) };

            if (req.Method == HttpMethod.Put)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        token = "t1",
                        link = "http://seaf.test/d/t1/",
                        repo_id = "lib",
                        path = "/a",
                        expire_date = expiresLater,
                        is_expired = false,
                        password = "",
                        permissions = new { can_edit = false, can_download = true, can_upload = false }
                    }))
                };

            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        token = "t2",
                        link = "http://seaf.test/d/t2/",
                        repo_id = "lib",
                        path = "/a",
                        expire_date = expiresLater,
                        is_expired = false,
                        password = "secret-password",
                        permissions = new { can_edit = false, can_download = true, can_upload = false }
                    }))
                };

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var s = Session(handler, retries: 0);

        var (reused, created1) = await s.ShareLinks.GetOrCreateAsync("lib", "/a", new SeafShareLinkOptions { CanUpload = false });
        Assert.False(created1);
        Assert.Equal("t1", reused.Token);
        Assert.True(reused.Permissions.CanUpload);

        calls.Clear();
        var (updated, created2) = await s.ShareLinks.GetOrCreateAsync("lib", "/a", new SeafShareLinkOptions { CanUpload = false, RequireMatchingPermissions = true });
        Assert.False(created2);
        Assert.False(updated.Permissions.CanUpload);
        Assert.Contains((HttpMethod.Put, "/sub/api/v2.1/share-links/t1/"), calls);

        calls.Clear();
        var (recreated, created3) = await s.ShareLinks.GetOrCreateAsync("lib", "/a", new SeafShareLinkOptions { Password = "secret-password", RequireMatchingPermissions = true });
        Assert.True(created3);
        Assert.Equal("t2", recreated.Token);
        Assert.Contains((HttpMethod.Delete, "/sub/api/v2.1/share-links/t1/"), calls);
        Assert.Contains((HttpMethod.Post, "/sub/api/v2.1/share-links/"), calls);

        calls.Clear();
        var (refreshed, created4) = await s.ShareLinks.GetOrCreateAsync("lib", "/a", new SeafShareLinkOptions { MinRemainingLifetime = TimeSpan.FromMinutes(30), ExpiresAt = expiresLater });
        Assert.True(created4);
        Assert.Equal("t2", refreshed.Token);
        Assert.Contains((HttpMethod.Delete, "/sub/api/v2.1/share-links/t1/"), calls);
    }
}
