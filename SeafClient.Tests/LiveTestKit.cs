using System.Collections.Concurrent;
using System.Text;

namespace SeafClient.Tests;

/// <summary>Records every HTTP request a session sends, so tests can assert how many requests a batch produced.</summary>
public sealed class RecordingHandler() : DelegatingHandler(new SocketsHttpHandler())
{
    public ConcurrentQueue<(HttpMethod Method, string Path)> Requests { get; } = new();

    public int Count(HttpMethod method, string pathSuffix)
        => Requests.Count(r => r.Method == method && r.Path.EndsWith(pathSuffix, StringComparison.Ordinal));

    public void Clear() => Requests.Clear();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Enqueue((request.Method, request.RequestUri!.AbsolutePath));
        return base.SendAsync(request, ct);
    }
}

/// <summary>Shared helpers for live tests.</summary>
public static class LiveKit
{
    public static Uri ServerUri => new(LiveServer.Url!);

    public static SeafClientOptions Recording(out RecordingHandler handler)
    {
        handler = new RecordingHandler();
        return new SeafClientOptions { HttpClient = new HttpClient(handler) };
    }

    public static string Unique(string prefix, int length = 28) => $"{prefix}-{Guid.NewGuid():N}"[..length];

    public static async Task Upload(SeafSession s, string libraryId, string dir, string name, string text)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await s.Files.UploadAsync(libraryId, dir, name, ms, new SeafUploadOptions { CreateParents = true, Replace = true });
    }

    /// <summary>Other active users on the server (users cannot be created on the test server because of its license).</summary>
    public static async Task<IReadOnlyList<string>> OtherUsersAsync(SeafSession s)
    {
        var me = (await s.Server.GetAccountInfoAsync()).Email;
        var page = await s.AdminUsers.ListAsync(1, 100);
        return page.Items.Where(u => u.IsActive && !string.Equals(u.Email, me, StringComparison.OrdinalIgnoreCase)).Select(u => u.Email).ToList();
    }

    /// <summary>Names "f0.txt" .. "f{n-1}.txt".</summary>
    public static string[] Names(int n) => Enumerable.Range(0, n).Select(i => $"f{i}.txt").ToArray();

    public static async Task<SeafLibrary> TempLibrary(SeafSession s, string prefix = "SeafClientTmp", string? password = null)
        => await s.Libraries.CreateAsync(Unique(prefix), password: password);

    /// <summary>Metadata indexing is asynchronous: poll the "All files" view until <paramref name="count"/> records show up.</summary>
    public static async Task<IReadOnlyList<SeafMetadataRecord>> WaitForRecords(SeafSession s, string libraryId, int count)
    {
        var view = (await s.Metadata.Views.ListAsync(libraryId)).Views.First();
        IReadOnlyList<SeafMetadataRecord> records = [];
        for (var i = 0; i < 60; i++)
        {
            records = (await s.Metadata.ListRecordsAsync(libraryId, view.Id)).Records.Where(r => !r.IsDir).ToList();
            if (records.Count >= count) return records;
            await Task.Delay(500);
        }
        return records;
    }
}
