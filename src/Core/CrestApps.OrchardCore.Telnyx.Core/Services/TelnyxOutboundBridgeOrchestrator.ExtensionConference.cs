using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Connecting an internal extension call through a conference, and noting on the agent's leg that its party answered.
/// </summary>
/// <remarks>
/// <para>
/// Two WebRTC legs bridged directly negotiate media but pass audio one way on Telnyx. A conference mixes them, and each
/// WebRTC leg gets normal two-way media with the mixer. The conference is named after the agent's leg,
/// <c>ext-{agent leg}</c>, so both legs resolve the same one.
/// </para>
/// <para>
/// The conference is made from the agent's own leg, and the colleague joins it as an ordinary participant. It used to be
/// made from the colleague's leg, with the agent's leg joined with <c>end_conference_on_exit</c>. On Telnyx a call that
/// created a conference is hung up (cause <c>time_limit</c>) when that conference is ended, even after it has left it
/// and joined another: live, a colleague merged into a conference with a dialed number was hung up the moment the agent
/// left and the extension call's conference ended, and the dialed party was left alone. Made from the agent's leg, the
/// only call its end can take down is the agent's, which is ending anyway. Nothing needs <c>end_conference_on_exit</c>:
/// the agent's leg ending releases the colleague (<see cref="ReleaseRemotePartyAsync"/>), and the colleague's ending
/// hangs up the agent's leg.
/// </para>
/// </remarks>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    private async Task ConnectExtensionViaConferenceAsync(
        string agentLegCallControlId,
        string destinationLegCallControlId,
        CancellationToken cancellationToken)
    {
        var conferenceName = $"ext-{agentLegCallControlId}";

        try
        {
            var conferenceId = await EnsureConferenceAsync(conferenceName, agentLegCallControlId, cancellationToken);

            if (string.IsNullOrWhiteSpace(conferenceId))
            {
                _logger.LogError(
                    "Could not resolve the Telnyx conference '{ConferenceName}' to connect an internal extension call.",
                    conferenceName.SanitizeLogValue());

                return;
            }

            var joinResult = await _apiClient.JoinConferenceAsync(
                conferenceId,
                destinationLegCallControlId,
                endConferenceOnExit: false,
                commandId: $"ext-join-{destinationLegCallControlId}",
                cancellationToken);

            if (!joinResult.Succeeded && !TelnyxApiErrors.IsAlreadyInConference(joinResult))
            {
                _logger.LogError(
                    "Telnyx rejected joining the colleague's leg to conference '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                    conferenceName.SanitizeLogValue(),
                    joinResult.StatusCode,
                    joinResult.ErrorBody.SanitizeLogValue());

                return;
            }

            await MarkPeerAnsweredAsync(agentLegCallControlId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while connecting an internal extension call through a Telnyx conference.");
        }
    }

    // The party of a number dialed from the soft phone, or an extension call's colleague, answered: the agent's leg says
    // so from now on, and the call can be merged. The leg is read first, so what else it carries is kept.
    private async Task MarkPeerAnsweredAsync(string agentLegCallControlId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentLegCallControlId))
        {
            return;
        }

        try
        {
            var status = await _apiClient.GetCallStatusAsync(agentLegCallControlId, cancellationToken);

            if (!status.Succeeded ||
                !TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var state) ||
                state.PeerAnswered != false)
            {
                return;
            }

            state.PeerAnswered = true;

            var updated = await _apiClient.UpdateClientStateAsync(agentLegCallControlId, state.ToClientStateJson(), cancellationToken);

            if (!updated.Succeeded)
            {
                _logger.LogWarning(
                    "Telnyx refused to note on the agent leg {AgentLeg} that its party answered ({StatusCode}); the call cannot be merged.",
                    agentLegCallControlId.SanitizeLogValue(),
                    updated.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while noting on the agent leg {AgentLeg} that its party answered.", agentLegCallControlId.SanitizeLogValue());
        }
    }
}
