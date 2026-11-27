using SeafClient.Api.RepoToken;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient;

/// <summary>
/// A connection scoped to a single library through a repo API token (<c>/api/v2.1/via-repo-token/</c>).
/// Tokens are created by the library owner with <see cref="Api.LibrariesApi.CreateApiTokenAsync"/>; they grant
/// <c>r</c> or <c>rw</c> access to that one library only, without a user account. Thread-safe.
/// </summary>
/// <example>
/// <code>
/// using var repo = SeafRepoTokenSession.FromToken(new Uri("https://cloud.example.com"), token);
/// var info = await repo.GetInfoAsync();
/// await repo.Files.UploadAsync("/inbox", "report.pdf", stream);
/// </code>
/// </example>
public sealed class SeafRepoTokenSession : IDisposable
{
    private readonly SeafTransport _transport;
    private readonly RepoTokenContext _context;

    private SeafRepoTokenSession(SeafTransport transport)
    {
        _transport = transport;
        _context = new RepoTokenContext(transport);
        Directories = new RepoTokenDirectoriesApi(_context);
        Files = new RepoTokenFilesApi(_context);
        Items = new RepoTokenItemsApi(_context);
        ShareLinks = new RepoTokenShareLinksApi(_context);
        Metadata = new RepoTokenMetadataApi(_context);
    }

    /// <summary>Creates a session from a repo API token (no request is made).</summary>
    public static SeafRepoTokenSession FromToken(Uri serverUri, string repoToken, SeafClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(serverUri);
        ArgumentException.ThrowIfNullOrEmpty(repoToken);
        return new SeafRepoTokenSession(new SeafTransport(serverUri, options ?? new SeafClientOptions()) { Token = repoToken });
    }

    /// <summary>The server base URI (always ends with '/').</summary>
    public Uri ServerUri => _transport.ServerUri;

    /// <summary>The repo API token used for this session.</summary>
    public string Token => _transport.Token ?? "";

    public RepoTokenDirectoriesApi Directories { get; }
    public RepoTokenFilesApi Files { get; }

    /// <summary>Batch copy/move/delete within the library.</summary>
    public RepoTokenItemsApi Items { get; }

    public RepoTokenShareLinksApi ShareLinks { get; }

    /// <summary>Metadata records, views and tags of the library (requires metadata to be enabled on it).</summary>
    public RepoTokenMetadataApi Metadata { get; }

    /// <summary>Returns basic information about the token's library.</summary>
    /// <exception cref="SeafException">401 if the token is invalid or was revoked.</exception>
    public async Task<SeafRepoInfo> GetInfoAsync(CancellationToken ct = default)
    {
        var info = await _transport.GetAsync<SeafRepoInfo>("api/v2.1/via-repo-token/repo-info/", ct).ConfigureAwait(false);
        _context.LibraryId ??= info.Id;
        return info;
    }

    /// <summary>Returns true if the token is accepted by the server, false on 401.</summary>
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try { await GetInfoAsync(ct).ConfigureAwait(false); return true; }
        catch (SeafException e) when (e.IsUnauthorized) { return false; }
    }

    public void Dispose() => _transport.Dispose();
}

/// <summary>Shared state of a repo-token session: the transport and the (lazily discovered) library id.</summary>
internal sealed class RepoTokenContext(SeafTransport transport)
{
    public SeafTransport T { get; } = transport;
    public string? LibraryId { get; set; }

    public const string Base = "api/v2.1/via-repo-token/";

    /// <summary>The token's library id, fetched once from <c>repo-info</c>.</summary>
    public async Task<string> GetLibraryIdAsync(CancellationToken ct)
    {
        if (LibraryId != null) return LibraryId;
        var info = await T.GetAsync<SeafRepoInfo>(Base + "repo-info/", ct).ConfigureAwait(false);
        return LibraryId ??= info.Id;
    }
}
