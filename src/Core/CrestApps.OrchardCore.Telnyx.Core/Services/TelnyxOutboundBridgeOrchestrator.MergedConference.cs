using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A merged conference that is down to one party.
/// </summary>
/// <remarks>
/// The agent leaving a merge ends it when one party is left (see TelnyxTelephonyProvider.ConferenceLeave.cs), but the
/// parties can go on their own afterwards: live, an agent merged a number and a colleague and left, and when the colleague
/// hung up the number stayed on, connected to nobody, until they hung up themselves. So whenever a party hangs up out of a
/// merge's conference and only one is left, the conference is ended, which hangs that one up.
/// </remarks>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    // The conferences a merge makes are named for the call that leads them (see TelnyxTelephonyProvider.Merge.cs).
    private const string MergedConferencePrefix = "conf-";

    private static bool IsConferenceParticipantLeft(TelnyxCallEvent callEvent)
        => string.Equals(callEvent.EventType?.Trim(), "conference.participant.left", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(callEvent.ConferenceId);

    private async Task EndMergedConferenceLeftWithOneAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return;
        }

        try
        {
            // A supervised call's conference drops to one participant on its way back to its bridge, and an extension
            // call's own conference ends by its own rules: only a merge's is ended here.
            var name = await _apiClient.GetConferenceNameAsync(callEvent.ConferenceId, cancellationToken);

            if (name is null || !name.StartsWith(MergedConferencePrefix, StringComparison.Ordinal))
            {
                return;
            }

            // A leg that left and is still up is being moved -- a merge undone, a call joined elsewhere -- and the
            // conference is being rearranged, not abandoned.
            var leaving = await _apiClient.GetCallStatusAsync(callEvent.CallControlId, cancellationToken);

            if (!leaving.Succeeded || leaving.IsAlive)
            {
                return;
            }

            var participants = await _apiClient.ReadJoinedConferenceParticipantsAsync(callEvent.ConferenceId, cancellationToken);
            var remaining = participants?
                .Where(participant => !string.Equals(participant.CallControlId, callEvent.CallControlId, StringComparison.Ordinal))
                .ToList();

            if (remaining is not { Count: 1 })
            {
                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Leg '{LegId}' hung up out of merged conference '{ConferenceName}', leaving '{RemainingLegId}' alone in it; the conference is ended.",
                    callEvent.CallControlId.SanitizeLogValue(),
                    name.SanitizeLogValue(),
                    remaining[0].CallControlId.SanitizeLogValue());
            }

            var ended = await _apiClient.EndConferenceAsync(callEvent.ConferenceId, cancellationToken);

            if (!ended.Succeeded)
            {
                _logger.LogWarning(
                    "Telnyx refused to end merged conference '{ConferenceName}' ({StatusCode}); its last party is hung up instead.",
                    name.SanitizeLogValue(),
                    ended.StatusCode);

                await _apiClient.HangupAsync(remaining[0].CallControlId, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The merged conference '{ConferenceId}' could not be read after a party left it.", callEvent.ConferenceId.SanitizeLogValue());
        }
    }
}
