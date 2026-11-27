using System.Net;

namespace SeafClient.Tests;

public class DiAndAuthUnitTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => respond(request, ct);
    }

    private sealed class TrackingHttpClient(HttpMessageHandler handler) : HttpClient(handler)
    {
        public bool IsDisposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            if (disposing) IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task HttpClientFactory_IsInvokedPerRequest_AndClientsAreNotDisposed()
    {
        var created = new List<TrackingHttpClient>();
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("\"pong\"")
        }));

        var session = SeafSession.FromToken(new Uri("http://seaf.test/"), "tok", new SeafClientOptions
        {
            HttpClientFactory = () =>
            {
                var client = new TrackingHttpClient(handler);
                created.Add(client);
                return client;
            }
        });

        Assert.True(await session.Server.PingAsync());
        Assert.True(await session.Server.PingAsync());
        Assert.Equal(2, created.Count);

        session.Dispose();
        Assert.All(created, c => Assert.False(c.IsDisposed));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.Server.PingAsync());
    }

    [Fact]
    public void HttpClientAndFactory_AreMutuallyExclusive()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        Assert.Throws<ArgumentException>(() => SeafSession.FromToken(new Uri("http://seaf.test/"), "tok", new SeafClientOptions
        {
            HttpClient = new HttpClient(handler),
            HttpClientFactory = () => new HttpClient(handler)
        }));
    }

    [Fact]
    public async Task Create_AcquiresTokenLazilySingleFlight_AndRefreshesOnceOn401()
    {
        var loginCount = 0;
        var currentToken = "tok-1";

        var handler = new StubHandler(async (req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath == "/api2/auth-token/")
            {
                var n = Interlocked.Increment(ref loginCount);
                await Task.Delay(20, ct);
                currentToken = $"tok-{n}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"token":"{{currentToken}}"}""")
                };
            }

            if (req.Headers.Authorization?.Parameter != currentToken)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("""{"detail":"Invalid token"}""")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"pong\"") };
        });

        using var s = SeafSession.Create(new Uri("http://seaf.test/"), "user@test.com", "pw",
            new SeafClientOptions { HttpClient = new HttpClient(handler), MaxRetries = 0 });

        Assert.Equal(0, loginCount);
        Assert.Equal("", s.AuthToken);

        var pingGate = new SemaphoreSlim(0, 8);
        var pingTasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await pingGate.WaitAsync();
            return await s.Server.AuthPingAsync();
        })).ToArray();
        pingGate.Release(8);
        Assert.All(await Task.WhenAll(pingTasks), Assert.True);
        Assert.Equal(1, loginCount);
        Assert.Equal("tok-1", s.AuthToken);

        currentToken = "expired-on-server";
        var refreshGate = new SemaphoreSlim(0, 6);
        var refreshTasks = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            await refreshGate.WaitAsync();
            return await s.Server.AuthPingAsync();
        })).ToArray();
        refreshGate.Release(6);
        Assert.All(await Task.WhenAll(refreshTasks), Assert.True);

        Assert.Equal(2, loginCount);
        Assert.Equal("tok-2", s.AuthToken);
    }
}
