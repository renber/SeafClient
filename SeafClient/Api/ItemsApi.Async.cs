using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

public sealed partial class ItemsApi
{
    /// <summary>
    /// Starts copying <paramref name="names"/> from <paramref name="sourceDir"/> into <paramref name="destinationDir"/> without
    /// waiting. Use for large copies between libraries; poll with <see cref="GetProgressAsync"/> or <see cref="WaitAsync"/>.
    /// </summary>
    /// <remarks>Inside a single library Seafile completes the copy before answering and returns an empty task id
    /// (<see cref="SeafAsyncTask.IsCompleted"/>).</remarks>
    public Task<SeafAsyncTask> StartCopyAsync(string sourceLibraryId, string sourceDir, IEnumerable<string> names, string destinationLibraryId, string destinationDir, CancellationToken ct = default)
        => StartAsync("api/v2.1/repos/async-batch-copy-item/", sourceLibraryId, sourceDir, names, destinationLibraryId, destinationDir, ct);

    /// <summary>Starts moving entries without waiting. See <see cref="StartCopyAsync"/>.</summary>
    public Task<SeafAsyncTask> StartMoveAsync(string sourceLibraryId, string sourceDir, IEnumerable<string> names, string destinationLibraryId, string destinationDir, CancellationToken ct = default)
        => StartAsync("api/v2.1/repos/async-batch-move-item/", sourceLibraryId, sourceDir, names, destinationLibraryId, destinationDir, ct);

    /// <summary>Returns the progress of an asynchronous copy/move.</summary>
    /// <remarks>
    /// Seafile forgets a task once it has reported the final state: querying a finished task a second time fails with a
    /// 500 "Error". Poll from one place only, and stop at the first <see cref="SeafTaskProgress.IsFinished"/> result.
    /// </remarks>
    public Task<SeafTaskProgress> GetProgressAsync(string taskId, CancellationToken ct = default)
        => _t.GetAsync<SeafTaskProgress>(Url.Of("api/v2.1/query-copy-move-progress/").Q("task_id", taskId), ct);

    /// <summary>Cancels a running asynchronous copy/move. Canceling a finished task is accepted and does nothing.</summary>
    public Task CancelAsync(string taskId, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, "api/v2.1/copy-move-task/", SeafTransport.Form(("task_id", taskId)), ct);

    /// <summary>
    /// Polls a task until it finishes and returns the final progress. Throws a <see cref="SeafException"/> if the task failed
    /// or was canceled. Canceling <paramref name="ct"/> stops waiting but does not cancel the server task.
    /// </summary>
    public async Task<SeafTaskProgress> WaitAsync(SeafAsyncTask task, TimeSpan? pollInterval = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.IsCompleted) return SeafTaskProgress.CompletedSynchronously;

        var delay = pollInterval ?? TimeSpan.FromMilliseconds(500);
        while (true)
        {
            var progress = await GetProgressAsync(task.TaskId, ct).ConfigureAwait(false);
            if (progress.Successful) return progress;
            if (progress.Failed || progress.Canceled)
                throw new SeafException(System.Net.HttpStatusCode.InternalServerError,
                    $"Copy/move task {task.TaskId} {(progress.Canceled ? "was canceled" : "failed")}.");
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    private async Task<SeafAsyncTask> StartAsync(string url, string srcRepo, string srcDir, IEnumerable<string> names, string dstRepo, string dstDir, CancellationToken ct)
    {
        var list = names.ToArray();
        if (list.Length == 0) return new SeafAsyncTask();
        return await _t.SendAsync<SeafAsyncTask>(HttpMethod.Post, url, SeafTransport.Json(new
        {
            src_repo_id = srcRepo,
            src_parent_dir = SeafPath.Normalize(srcDir),
            src_dirents = list,
            dst_repo_id = dstRepo,
            dst_parent_dir = SeafPath.Normalize(dstDir),
        }), ct).ConfigureAwait(false);
    }

    // ---------------- zip download ----------------

    /// <summary>
    /// Asks the server to zip <paramref name="names"/> (files or directories inside <paramref name="parentDir"/>) and returns
    /// the one-time download URL. Pass a single directory name to download a whole folder.
    /// </summary>
    public async Task<SeafZipTask> CreateZipAsync(string libraryId, string parentDir, IEnumerable<string> names, CancellationToken ct = default)
    {
        var url = Url.Of($"api/v2.1/repos/{Url.Seg(libraryId)}/zip-task/").Q("parent_dir", SeafPath.Normalize(parentDir));
        var count = 0;
        foreach (var name in names) { url.Q("dirents", name); count++; }
        if (count == 0) throw new ArgumentException("At least one entry name is required.", nameof(names));

        var task = await _t.GetAsync<SeafZipTask>(url, ct).ConfigureAwait(false);
        return task with { DownloadUri = _t.Resolve($"seafhttp/zip/{Url.Seg(task.ZipToken)}") };
    }

    /// <summary>
    /// Returns the progress of a zip task, or null when the server does not track it. Seafile 11+ streams zips on the fly,
    /// so this is normally null and the download can start immediately.
    /// </summary>
    public async Task<SeafZipProgress?> GetZipProgressAsync(string zipToken, CancellationToken ct = default)
    {
        try
        {
            return await _t.GetAsync<SeafZipProgress>(Url.Of("api/v2.1/query-zip-progress/").Q("token", zipToken), ct).ConfigureAwait(false);
        }
        catch (SeafException e) when (e.IsNotFound)
        {
            return null;
        }
    }

    /// <summary>Zips the entries on the server and opens the zip as a stream (dispose it when done).</summary>
    public async Task<Stream> OpenZipAsync(string libraryId, string parentDir, IEnumerable<string> names, CancellationToken ct = default)
    {
        var task = await CreateZipAsync(libraryId, parentDir, names, ct).ConfigureAwait(false);

        // Older servers build the zip first; wait until it is ready when progress is reported.
        while (await GetZipProgressAsync(task.ZipToken, ct).ConfigureAwait(false) is { } p && !p.IsFinished)
            await Task.Delay(500, ct).ConfigureAwait(false);

        var response = await _t.SendRawAsync(HttpMethod.Get, task.DownloadUri.AbsoluteUri, null, HttpCompletionOption.ResponseHeadersRead, ct, authenticate: false).ConfigureAwait(false);
        return await SeafTransport.ReadStreamAsync(response, ct).ConfigureAwait(false);
    }

    /// <summary>Zips the entries on the server and writes the zip into <paramref name="destination"/>.</summary>
    public async Task DownloadZipAsync(string libraryId, string parentDir, IEnumerable<string> names, Stream destination, CancellationToken ct = default)
    {
        await using var zip = await OpenZipAsync(libraryId, parentDir, names, ct).ConfigureAwait(false);
        await zip.CopyToAsync(destination, ct).ConfigureAwait(false);
    }
}
