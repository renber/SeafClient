using SeafClient.Api;
using SeafClient.Internal;

namespace SeafClient;

/// <summary>
/// An authenticated connection to a Seafile server. Thread-safe; create one per server/account and reuse it.
/// </summary>
/// <example>
/// <code>
/// using var seaf = await SeafSession.LoginAsync(new Uri("https://cloud.example.com"), "me@example.com", "secret");
/// var libs = await seaf.Libraries.ListAsync(SeafLibraryType.Mine);
/// await seaf.Directories.EnsureAsync(libs[0].Id, "/reports/2026");
/// await seaf.Files.UploadAsync(libs[0].Id, "/reports/2026", "q1.pdf", stream);
/// </code>
/// </example>
public sealed class SeafSession : IDisposable
{
    private readonly SeafTransport _transport;

    private SeafSession(SeafTransport transport, string? username)
    {
        _transport = transport;
        Username = username;

        Server = new ServerApi(transport);
        Libraries = new LibrariesApi(transport);
        Items = new ItemsApi(transport);
        Directories = new DirectoriesApi(transport, Items);
        Files = new FilesApi(transport);
        ShareLinks = new ShareLinksApi(transport);
        UploadLinks = new UploadLinksApi(transport);
        FolderPermissions = new FolderPermissionsApi(transport);
        Starred = new StarredApi(transport);
        Groups = new GroupsApi(transport);
        Metadata = new MetadataApi(transport);
        Account = new AccountApi(transport);
        Search = new SearchApi(transport);
        FileComments = new FileCommentsApi(transport);
        FileTags = new FileTagsApi(transport);
        Invitations = new InvitationsApi(transport);
        Admin = new AdminApi(transport);
    }

    /// <summary>The server base URI (always ends with '/').</summary>
    public Uri ServerUri => _transport.ServerUri;

    /// <summary>The API token used for this session.</summary>
    public string AuthToken => _transport.Token ?? "";

    /// <summary>The username used to log in, if the session was created by <see cref="LoginAsync"/>.</summary>
    public string? Username { get; }

    public ServerApi Server { get; }
    public LibrariesApi Libraries { get; }
    public DirectoriesApi Directories { get; }
    public FilesApi Files { get; }
    public ItemsApi Items { get; }
    public ShareLinksApi ShareLinks { get; }
    public UploadLinksApi UploadLinks { get; }

    /// <summary>Per-user / per-group permissions on sub-folders of a library (Pro).</summary>
    public FolderPermissionsApi FolderPermissions { get; }

    public StarredApi Starred { get; }
    public GroupsApi Groups { get; }
    public MetadataApi Metadata { get; }

    /// <summary>The current user's profile, avatar, devices and activity feed.</summary>
    public AccountApi Account { get; }

    public SearchApi Search { get; }
    public FileCommentsApi FileComments { get; }

    /// <summary>Legacy library/file tags (<c>repo-tags</c>). For metadata-server tags see <see cref="Metadata"/>.</summary>
    public FileTagsApi FileTags { get; }

    public InvitationsApi Invitations { get; }

    /// <summary>System administration (staff accounts only).</summary>
    public AdminApi Admin { get; }

    /// <summary>
    /// Obtains an API token with username/password and returns a session using it.
    /// </summary>
    /// <param name="oneTimePassword">Two-factor code, if the account has 2FA enabled.</param>
    /// <exception cref="SeafException">Wrong credentials (400) or server error.</exception>
    public static async Task<SeafSession> LoginAsync(Uri serverUri, string username, string password, SeafClientOptions? options = null, string? oneTimePassword = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serverUri);
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentNullException.ThrowIfNull(password);

        var transport = new SeafTransport(serverUri, options ?? new SeafClientOptions());
        try
        {
            var token = await ObtainTokenAsync(transport, username, password, oneTimePassword, ct).ConfigureAwait(false);
            transport.Token = token;
            return new SeafSession(transport, username);
        }
        catch
        {
            transport.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a session configured with credentials without performing network I/O. The API token is acquired lazily
    /// on the first authenticated request (single-flight) and automatically refreshed once if the server returns 401.
    /// Ideal for dependency injection singletons.
    /// </summary>
    public static SeafSession Create(Uri serverUri, string username, string password, SeafClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(serverUri);
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentNullException.ThrowIfNull(password);

        SeafTransport transport = null!;
        transport = new SeafTransport(serverUri, options ?? new SeafClientOptions(),
            ct => ObtainTokenAsync(transport, username, password, null, ct));
        return new SeafSession(transport, username);
    }

    /// <summary>Creates a session from an existing API token (no request is made).</summary>
    public static SeafSession FromToken(Uri serverUri, string token, SeafClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(serverUri);
        ArgumentException.ThrowIfNullOrEmpty(token);
        return new SeafSession(new SeafTransport(serverUri, options ?? new SeafClientOptions()) { Token = token }, null);
    }

    /// <summary>Requests an API token without creating a session.</summary>
    public static async Task<string> GetTokenAsync(Uri serverUri, string username, string password, SeafClientOptions? options = null, string? oneTimePassword = null, CancellationToken ct = default)
    {
        using var transport = new SeafTransport(serverUri, options ?? new SeafClientOptions());
        return await ObtainTokenAsync(transport, username, password, oneTimePassword, ct).ConfigureAwait(false);
    }

    private static async Task<string> ObtainTokenAsync(SeafTransport transport, string username, string password, string? otp, CancellationToken ct)
    {
        using var response = await transport.SendRawAsync(HttpMethod.Post, "api2/auth-token/", () =>
        {
            var content = new FormUrlEncodedContent([new("username", username), new("password", password)]);
            return content;
        }, HttpCompletionOption.ResponseContentRead, ct, authenticate: false, configure: otp == null ? null : r => r.Headers.Add("X-SEAFILE-OTP", otp)).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return SeafJson.Deserialize<TokenResponse>(json).Token;
    }

    private sealed record TokenResponse { public string Token { get; init; } = ""; }

    public void Dispose() => _transport.Dispose();
}
