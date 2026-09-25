using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Parks a Contact Center caller before the agent's leg is released, so the release does not take the caller with it.
/// </summary>
/// <remarks>
/// <para>
/// The agent's leg is bridged to the caller with the bridge issued on the agent's leg and
/// <c>park_after_unbridge=self</c>. Telnyx applies that to the leg the command was sent on only: the agent's leg is
/// parked when the bridge ends, and the caller's leg keeps the default, which is to be hung up. Hanging up the agent's
/// leg ends the bridge, so the caller went with it.
/// </para>
/// <para>
/// Telnyx has no command that simply unbridges a call. Creating a conference from the caller's leg moves the caller
/// into it, which takes them out of the agent's bridge and parks the agent's leg (the same move a consult makes), and
/// leaving the conference returns the caller to the parked state. A parked caller hears hold music, is joined to the
/// next agent exactly like a caller who has just come in, and no longer depends on the agent's leg at all.
/// </para>
/// </remarks>
public sealed partial class TelnyxContactCenterVoiceProvider : IContactCenterVoiceCallerParkProvider
{
    /// <inheritdoc/>
    public async Task<bool> ParkCallerAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return false;
        }

        var callerCallControlId = providerCallId.Trim();

        // A new name every time: the same call can be parked again by a later transfer, and Telnyx collapses a
        // repeated command_id on one leg into the first.
        var conferenceName = $"cc-park-{Guid.NewGuid():N}";

        var conference = await _apiClient.CreateConferenceAsync(conferenceName, callerCallControlId, commandId: conferenceName, cancellationToken);

        if (!conference.Succeeded || string.IsNullOrWhiteSpace(conference.ConferenceId))
        {
            _logger.LogError(
                "Telnyx refused to take caller '{CallControlId}' out of the agent's bridge with status code {StatusCode}. Response: {Response}",
                callerCallControlId.SanitizeLogValue(),
                conference.StatusCode,
                conference.ErrorBody.SanitizeLogValue());

            return false;
        }

        var leave = await _apiClient.LeaveConferenceAsync(conference.ConferenceId, callerCallControlId, cancellationToken);

        if (!leave.Succeeded)
        {
            // The caller is alone in a conference of their own, which is still out of the agent's bridge: the agent's
            // leg can be hung up without them.
            _logger.LogWarning(
                "Telnyx returned {StatusCode} taking caller '{CallControlId}' out of their holding conference. Response: {Response}",
                leave.StatusCode,
                callerCallControlId.SanitizeLogValue(),
                leave.ErrorBody.SanitizeLogValue());
        }

        return true;
    }
}
