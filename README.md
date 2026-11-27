# SeafClient

A strongly-typed, async .NET client for the [Seafile](https://www.seafile.com) Web API.

Version 2 is a ground-up rewrite targeting **Seafile 11–14** (tested against Seafile 14.0.9 Pro):

- `net8.0` / `net10.0`, `System.Text.Json`, no third-party dependencies
- Resource-grouped API: `session.Files`, `session.Directories`, `session.ShareLinks`, ...
- Uses the current `api/v2.1` endpoints, falling back to `api2` only where v2.1 has no equivalent
- Typed errors (`SeafException` with `StatusCode`, `IsNotFound`, `IsUnauthorized`, `IsForbidden`, `IsTransient`, plus `SeafConnectionException` and `SeafTimeoutException`), automatic retries on 429, 408, transient 5xx, and network/timeout errors
- Every call takes a `CancellationToken`; DI-safe lazy auth via `SeafSession.Create(...)` and `IHttpClientFactory` support

## Quick start

```csharp
using SeafClient;
using SeafClient.Models;

using var seaf = await SeafSession.LoginAsync(new Uri("https://cloud.example.com"), "me@example.com", password);

var lib = await seaf.Libraries.FindByNameAsync("TaxData")
          ?? await seaf.Libraries.CreateAsync("TaxData");

// Idempotent: never produces "2026 (1)".
await seaf.Directories.EnsureAsync(lib.Id, "/clients/42/2026");

await using var pdf = File.OpenRead("return.pdf");
var uploaded = await seaf.Files.UploadAsync(lib.Id, "/clients/42/2026", "return.pdf", pdf,
    new SeafUploadOptions { Replace = true, Progress = new Progress<long>(b => Console.WriteLine(b)) });

foreach (var entry in await seaf.Directories.ListAsync(lib.Id, "/clients/42", recursive: true))
    Console.WriteLine($"{entry.Type} {entry.Path} {entry.Size} {entry.Modified}");

var (link, created) = await seaf.ShareLinks.GetOrCreateAsync(lib.Id, uploaded.Path,
    new SeafShareLinkOptions { ExpiresAt = DateTimeOffset.Now.AddDays(7), Password = "s3cret-pass" });
```

An existing API token can be used directly: `SeafSession.FromToken(serverUri, token)`, or create a synchronous lazy-authenticating session for DI containers with `SeafSession.Create(serverUri, username, password, options)`.

### Library (repo) API tokens

A repo token grants access to a single library without a user account, e.g. for integrations:

```csharp
var token = await seaf.Libraries.CreateApiTokenAsync(lib.Id, "my-integration", SeafRepoTokenPermission.Rw);
using var repo = SeafRepoTokenSession.FromToken(serverUri, token.ApiToken);
await repo.Directories.EnsureAsync("/inbox");
await repo.Files.UploadAsync("/inbox", "a.txt", stream);
```

`SeafRepoTokenSession` offers `Directories`, `Files`, `Items`, `ShareLinks` and `Metadata` (records, views, tags) scoped to that library.
On Seafile 14 some tag operations (file tags, tag files, tag links) fail through a repo token server-side; use an account session for those.

## API overview

| Group | Highlights |
|---|---|
| `Server` | `PingAsync`, `AuthPingAsync`, `GetInfoAsync`, `GetAccountInfoAsync`, `GetAvatarUrlAsync` |
| `Account` | Profile get/update, `UploadAvatarAsync`, devices list/unlink, client-login token/URL, `ListActivitiesAsync` |
| `Libraries` | `ListAsync(type)`, `ListDistinctAsync`, `FindByNameAsync`, `SearchByNameAsync`, `GetAsync`, `CreateAsync`, `RenameAsync`, `DeleteAsync`, `DecryptAsync`, default library, owner / `TransferAsync`, history, commits, snapshots (`ListSnapshotDirectoryAsync`, `RevertToSnapshotAsync`), snapshot labels, history limit, trash (list/clean/restore), shared-with-me / `LeaveShareAsync`, sharing with users/groups (single, many, batch across libraries, folder shares, permission updates, `ListSharedFoldersAsync`) |
| `Directories` | `ListAsync` (filter, recursive), `GetAsync` / `TryGetAsync` / `ExistsAsync`, `CreateAsync`, `EnsureAsync`, `RenameAsync`, `CopyAsync`, `MoveAsync`, `MoveMergeAsync`, `RevertAsync`, `DeleteAsync` / `DeleteIfExistsAsync` |
| `Files` | `GetAsync` / `TryGetAsync` / `ExistsAsync`, `UploadAsync` / `UploadFileAsync`, `UpdateAsync`, `OpenReadAsync`, `DownloadAsync`, `ReadAllBytesAsync`, `ReadAllTextAsync`, download/upload links, `RenameAsync`, `CopyAsync`, `MoveAsync`, `DeleteAsync`, `CreateAsync`, `GetHistoryAsync`, revisions (`RevertAsync`, `OpenRevisionReadAsync`, ...), `LockAsync` / `UnlockAsync`, `GetThumbnailAsync` |
| `Items` | Batch `CopyAsync`, `MoveAsync`, `DeleteAsync`; async `StartCopyAsync` / `StartMoveAsync` / `GetProgressAsync` / `CancelAsync` / `WaitAsync`; zip downloads (`CreateZipAsync`, `OpenZipAsync`, `DownloadZipAsync`) |
| `ShareLinks` | `ListAsync`, `GetAsync`, `CreateAsync`, `GetOrCreateAsync`, `UpdateAsync`, `DeleteAsync` / `DeleteManyAsync`, `CreateAdditionalAsync`, `CreateBatchAsync`, `SendAsync`, `ListDirentsAsync`, authorized users/emails, `GetSmartLinkAsync` |
| `UploadLinks` | `ListAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync` / `DeleteManyAsync`, `SendAsync`, `GetUploadUrlAsync`, `UploadAsync` (through the link) |
| `FolderPermissions` | Sub-folder permissions for users and groups: list, add, update, remove |
| `Starred` | `ListAsync`, `StarAsync`, `UnstarAsync` |
| `Groups` | CRUD, `TransferAsync`, members (get, single and bulk add, remove), `SetAdminAsync`, `ListLibrariesAsync` |
| `FileComments` | List, count, create, get, update/resolve, delete, replies |
| `FileTags` | Legacy library tags and file tags (`repo-tags`) |
| `Search` | `SearchFilesAsync` (full-text; needs a search backend), `SearchFileNamesAsync`, `SearchUsersAsync` |
| `Invitations` | Guest invitations: list, create (single/many), get, delete, revoke |
| `Metadata` | `GetStatusAsync`, `SetEnabledAsync`, records (`GetRecordAsync`, `GetRecordByIdAsync`, `ListRecordsAsync`, `UpdateRecordsAsync` auto-batched, `UpdateRecordByPathAsync`), columns, details settings, `ExtractFileDetailsAsync`; `.Views` (CRUD, move, duplicate, view folders), `.Tags` (CRUD, file tags, tag links, merge) |
| `Admin` | `Users`, `Groups`, `Libraries` (incl. trash and history settings), `Departments`, `Logs`, `Organizations`, `Shares`, `ShareLinks`, `Notifications` (staff only) |

`SeafPath` normalizes and combines library paths (`SeafPath.Combine("42", "Uploads/", "/2026")` → `/42/Uploads/2026`).
All returned paths are normalized (leading `/`, no trailing `/`).

## Seafile behaviours worth knowing

- **mkdir never fails on an existing name**; Seafile silently creates `name (1)`. `Directories.CreateAsync` checks first and throws a
  409 `SeafException`; `Directories.EnsureAsync` is the idempotent variant.
- **Uploads auto-rename** unless `Replace = true`. `SeafUploadedFile.Name` / `.Path` hold the name actually stored.
- **File delete is idempotent** on the server; directory delete returns 404 for a missing folder (use `DeleteIfExistsAsync`).
- **File history and metadata records are indexed asynchronously**; they can take a few seconds to show up.
- **Downloads are decoded as UTF-8** by `ReadAllTextAsync` because the file server often declares a wrong charset.
- If Seafile's `SERVICE_URL` / `FILE_SERVER_ROOT` is not reachable from the client (common with Docker), set
  `SeafClientOptions.RewriteFileServerUrls = true` to rewrite upload/download links to the session's host.

## Options

```csharp
var options = new SeafClientOptions
{
    HttpClientFactory = () => httpClientFactory.CreateClient("seafile"), // recommended for DI; or pass HttpClient directly
    MaxRetries = 3,                                                       // 429 always; 408/502/503/504 + network/timeout errors for GET/HEAD/PUT/DELETE
    RetryBaseDelay = TimeSpan.FromSeconds(1),                             // exponential backoff with jitter unless the server sends Retry-After
    Timeout = TimeSpan.FromMinutes(5),                                    // only for the internally created HttpClient
};
```

## Tests

`SeafClient.Tests` contains unit tests and live integration tests. The live tests create (and delete) a temporary library
and group, and are skipped unless these environment variables are set:

```bash
export SEAFILE_TEST_URL=http://localhost:4500
export SEAFILE_TEST_USER=me@example.com
export SEAFILE_TEST_PASSWORD=...
dotnet test
```

The account should be a system admin for the admin-API tests.

## Migrating from 1.x

| 1.x | 2.x |
|---|---|
| `SeafSession.Establish(uri, user, char[] pwd)` | `SeafSession.LoginAsync(uri, user, pwd)` |
| `session.Ping()` | `session.Server.PingAsync()` |
| `ListLibraries()` + `ListSharedLibraries()` | `Libraries.ListAsync(type)` / `ListDistinctAsync()` / `FindByNameAsync()` |
| `ListDirectory(lib, path)` / `SendRequest(new ListDirectoryEntriesRequest(...))` | `Directories.ListAsync(lib, path, filter, recursive)` |
| `GetDirectoryDetail` / `CreateDirectory` / `DeleteDirectory` | `Directories.GetAsync` / `CreateAsync` or `EnsureAsync` / `DeleteAsync` |
| `GetFileDetail` (returned `SeafDirEntry`) | `Files.GetAsync` (returns `SeafFile`) |
| `UploadSingle(lib, dir, name, stream, createParents)` | `Files.UploadAsync(lib, dir, name, stream, new SeafUploadOptions { CreateParents = ... })` |
| `GetFileDownloadLink` | `Files.GetDownloadLinkAsync`, or `OpenReadAsync` / `ReadAllTextAsync` |
| `CopyFile` / `MoveFile` | `Files.CopyAsync` / `Files.MoveAsync` (directory destination) |
| `ListSharedLinks` / `CreateShareLink` / `DeleteShareLink(link.Id)` | `ShareLinks.ListAsync` / `CreateAsync` / `DeleteAsync(link.Token)` |
| `GetSmartLink` | `ShareLinks.GetSmartLinkAsync` |
| `SeafDirEntry.Timestamp` (`DateTime`, local) | `SeafDirEntry.Modified` (`DateTimeOffset?`) |
| `SeafException.SeafError.HttpStatusCode` | `SeafException.StatusCode` / `IsNotFound` |
| namespaces `SeafClient.Types`, `.Requests.*`, `.Exceptions` | `SeafClient.Models`; exceptions live in `SeafClient` |

## License

MIT. Originally created by René Bergelt.
