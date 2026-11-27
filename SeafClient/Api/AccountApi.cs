using System.Net.Http.Headers;
using System.Text;
using SeafClient.Internal;
using SeafClient.Models;

namespace SeafClient.Api;

/// <summary>The current user's profile, avatar, linked devices and activity feed.</summary>
public sealed class AccountApi
{
    private readonly SeafTransport _t;
    internal AccountApi(SeafTransport t) => _t = t;

    public Task<SeafUserProfile> GetProfileAsync(CancellationToken ct = default)
        => _t.GetAsync<SeafUserProfile>("api/v2.1/user/", ct);

    /// <summary>Updates profile fields and returns the new profile.</summary>
    /// <exception cref="SeafException">403 "Feature disabled." when the server does not let users edit that field.</exception>
    public Task<SeafUserProfile> UpdateProfileAsync(SeafProfileUpdate update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        return _t.SendAsync<SeafUserProfile>(HttpMethod.Put, "api/v2.1/user/", SeafTransport.Form(
            ("name", update.Name),
            ("contact_email", update.ContactEmail),
            ("telephone", update.Telephone),
            ("list_in_address_book", update.ListInAddressBook == null ? null : update.ListInAddressBook.Value ? "true" : "false")), ct);
    }

    /// <summary>
    /// Uploads a new avatar image and returns its URL. <paramref name="fileName"/>'s extension must be one the server allows
    /// (by default png, jpg, jpeg, gif). The server keeps the image; there is no API to go back to the default avatar.
    /// </summary>
    public async Task<string> UploadAvatarAsync(Stream image, string fileName, int avatarSize = 80, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        var start = image.CanSeek ? image.Position : -1;
        var attempt = 0;

        var result = await _t.SendAsync<AvatarResponse>(HttpMethod.Post, "api/v2.1/user-avatar/", () =>
        {
            if (attempt++ > 0)
            {
                if (start < 0) throw new InvalidOperationException("The avatar stream cannot be re-sent because it is not seekable.");
                image.Position = start;
            }

            var form = new MultipartFormDataContent("----SeafClient" + Guid.NewGuid().ToString("N"));
            form.HeaderEncodingSelector = (_, _) => Encoding.UTF8;
            var file = new StreamContent(image);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            file.Headers.TryAddWithoutValidation("Content-Disposition", $"form-data; name=\"avatar\"; filename=\"{fileName.Replace("\"", "\\\"")}\"");
            form.Add(file);
            var size = new ByteArrayContent(Encoding.UTF8.GetBytes(avatarSize.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            size.Headers.TryAddWithoutValidation("Content-Disposition", "form-data; name=\"avatar_size\"");
            form.Add(size);
            return form;
        }, ct).ConfigureAwait(false);
        return result.AvatarUrl;
    }

    /// <summary>Lists desktop and mobile clients linked to the account.</summary>
    public async Task<IReadOnlyList<SeafDevice>> ListDevicesAsync(CancellationToken ct = default)
        => await _t.GetAsync<List<SeafDevice>>("api2/devices/", ct).ConfigureAwait(false);

    /// <summary>
    /// Unlinks a client, revoking its token. With <paramref name="remoteWipe"/> the client also deletes its local copies of
    /// synced libraries the next time it connects. Seafile 14 answers 500 for a device that is not linked.
    /// </summary>
    public Task UnlinkDeviceAsync(string platform, string deviceId, bool remoteWipe = false, CancellationToken ct = default)
        => _t.SendAsync(HttpMethod.Delete, "api2/devices/", SeafTransport.Form(
            ("platform", platform), ("device_id", deviceId), ("wipe_device", remoteWipe ? "true" : null)), ct);

    /// <summary>Unlinks a client listed by <see cref="ListDevicesAsync"/>.</summary>
    public Task UnlinkDeviceAsync(SeafDevice device, bool remoteWipe = false, CancellationToken ct = default)
        => UnlinkDeviceAsync(device.Platform, device.DeviceId, remoteWipe, ct);

    /// <summary>
    /// Returns a short-lived, single-use token that logs a browser into the web UI as this user:
    /// open <c>{server}/client-login/?token={token}</c>.
    /// </summary>
    public async Task<string> GetClientLoginTokenAsync(CancellationToken ct = default)
        => (await _t.SendAsync<TokenResponse>(HttpMethod.Post, "api2/client-login/", null, ct).ConfigureAwait(false)).Token;

    /// <summary>Returns the URL that logs a browser into the web UI as this user (see <see cref="GetClientLoginTokenAsync"/>).</summary>
    public async Task<Uri> GetClientLoginUrlAsync(CancellationToken ct = default)
        => _t.Resolve(Url.Of("client-login/").Q("token", await GetClientLoginTokenAsync(ct).ConfigureAwait(false)));

    /// <summary>Returns one page of the activity feed (changes in libraries the user can access), newest first. Seafile Pro.</summary>
    public async Task<IReadOnlyList<SeafActivity>> ListActivitiesAsync(int page = 1, int perPage = 25, CancellationToken ct = default)
        => (await _t.GetAsync<ActivityResponse>(Url.Of("api/v2.1/activities/").Q("page", page).Q("per_page", perPage), ct).ConfigureAwait(false)).Events;

    private sealed record AvatarResponse { public string AvatarUrl { get; init; } = ""; }
    private sealed record TokenResponse { public string Token { get; init; } = ""; }
    private sealed record ActivityResponse { public List<SeafActivity> Events { get; init; } = []; }
}
