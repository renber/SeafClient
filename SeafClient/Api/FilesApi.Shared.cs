namespace SeafClient.Api;

public sealed partial class FilesApi
{
    // Upload/download plumbing reused by the repo-token API (SeafRepoTokenSession).

    internal static Func<HttpContent> BuildReplayableUpload(IEnumerable<(string Name, string Value)> fields, string fileName, Stream content, string? contentType, IProgress<long>? progress)
        => Replayable(content, () => BuildMultipart(fields, fileName, content, contentType, progress));
}
