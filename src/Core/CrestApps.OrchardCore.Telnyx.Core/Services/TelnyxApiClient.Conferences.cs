namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Conference participant commands: the mixer a warm transfer holds the caller in while the agent consults.
/// </summary>
public sealed partial class TelnyxApiClient
{
    /// <summary>
    /// Puts a conference participant on hold, playing them audio while they cannot hear the others.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="callControlId">The participant to hold.</param>
    /// <param name="audio">The audio to play: a URL, or the name of a clip already stored on Telnyx; <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> HoldConferenceParticipantAsync(
        string conferenceId,
        string callControlId,
        string audio = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["call_control_ids"] = new[] { callControlId },
        };

        if (!string.IsNullOrWhiteSpace(audio))
        {
            // The same rule as playback: a URL Telnyx fetches, or a clip it holds by name, and the wrong key for the
            // value is refused, which the caller hears as silence.
            var isUrl = Uri.TryCreate(audio, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

            body[isUrl ? "audio_url" : "media_name"] = audio;
        }

        return PostConferenceActionAsync(conferenceId, "hold", body, cancellationToken);
    }

    /// <summary>
    /// Takes a conference participant off hold.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="callControlId">The participant.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> UnholdConferenceParticipantAsync(
        string conferenceId,
        string callControlId,
        CancellationToken cancellationToken = default)
        => PostConferenceActionAsync(
            conferenceId,
            "unhold",
            new Dictionary<string, object>(StringComparer.Ordinal) { ["call_control_ids"] = new[] { callControlId } },
            cancellationToken);

    /// <summary>
    /// Takes a participant out of a conference without hanging it up; the call is left parked.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="callControlId">The participant.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<TelnyxApiResult> LeaveConferenceAsync(
        string conferenceId,
        string callControlId,
        CancellationToken cancellationToken = default)
        => PostConferenceActionAsync(
            conferenceId,
            "leave",
            new Dictionary<string, object>(StringComparer.Ordinal) { ["call_control_id"] = callControlId },
            cancellationToken);

    /// <summary>
    /// Sends a conference action. Not retried: none of these is safe to repeat blindly on a live call.
    /// </summary>
    /// <param name="conferenceId">The conference.</param>
    /// <param name="action">The Telnyx action name.</param>
    /// <param name="body">The action body.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxApiResult> PostConferenceActionAsync(
        string conferenceId,
        string action,
        IDictionary<string, object> body,
        CancellationToken cancellationToken = default)
    {
        var (result, _) = await SendAsync(
            HttpMethod.Post,
            $"conferences/{Uri.EscapeDataString(conferenceId ?? string.Empty)}/actions/{action}",
            body ?? new Dictionary<string, object>(StringComparer.Ordinal),
            retryable: false,
            cancellationToken);

        return result;
    }
}
