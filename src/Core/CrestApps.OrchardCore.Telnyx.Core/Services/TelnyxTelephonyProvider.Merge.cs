using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Merging calls into a Telnyx conference, and sending digits, on the leg that reaches the other party.
/// </summary>
/// <remarks>
/// <para>
/// For a number dialed from the soft phone the call the phone names is the agent's own leg, bridged to the dialed
/// party's leg with <c>park_after_unbridge=self</c>. Conferencing the agent's legs would join the agent to themselves
/// once per call. So the conference is made from the first call's dialed party (which parks the agent's first leg), the
/// agent's first leg joins it with <c>end_conference_on_exit</c> -- the agent leaving ends the conference -- and every
/// other call's dialed party joins it. The agent's other legs stay parked, one per participant: hanging one up ends that
/// participant (the orchestrator releases a leg's dialed party with it), and a participant hanging up releases its leg.
/// </para>
/// <para>
/// The first call's dialed party is detached from the agent's first leg as it moves, because that leg is now the agent
/// in the conference: the party leaving must not end the conference for everyone else.
/// </para>
/// </remarks>
public sealed partial class TelnyxTelephonyProvider
{
    /// <inheritdoc/>
    public async Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        var callIds = request?.GetCallIds();

        if (callIds is null || callIds.Count < 2)
        {
            return TelephonyResult.Failed(S["At least two calls are required to merge calls."].Value);
        }

        if (!_options.IsConfigured)
        {
            return NotConfigured();
        }

        var primaryCallId = callIds[0];
        var conferenceName = string.IsNullOrWhiteSpace(request.ConferenceName)
            ? $"conf-{primaryCallId}"
            : request.ConferenceName;

        try
        {
            // A merge that names its conference is adding calls to one already running, which the primary call is in:
            // the others join it. Creating another from the primary would take it out of the first.
            var conferenceId = string.IsNullOrWhiteSpace(request.ConferenceName)
                ? null
                : (await _apiClient.FindConferenceByNameAsync(conferenceName, cancellationToken)).ConferenceId;

            if (string.IsNullOrWhiteSpace(conferenceId))
            {
                conferenceId = await CreateConferenceFromAsync(conferenceName, primaryCallId, cancellationToken);

                if (conferenceId is null)
                {
                    return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
                }
            }

            foreach (var secondaryCallId in callIds.Skip(1))
            {
                // A dialed number joins as its dialed party; the agent's own leg for it stays parked.
                var bridge = await FindBridgedDialAsync(secondaryCallId, cancellationToken);
                var joinResult = await _apiClient.JoinConferenceAsync(conferenceId, bridge?.RemoteLegId ?? secondaryCallId, cancellationToken: cancellationToken);

                if (!joinResult.Succeeded)
                {
                    _logger.LogError(
                        "Telnyx rejected a conference join request with status code {StatusCode}. Response: {Response}",
                        joinResult.StatusCode,
                        joinResult.ErrorBody.SanitizeLogValue());

                    return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
                }
            }

            return TelephonyResult.Success(BuildCall(
                primaryCallId,
                CallState.Connected,
                new Dictionary<string, object>
                {
                    ["isConference"] = true,
                    ["conferenceId"] = conferenceId,
                    // The soft phone names this conference when it adds a call to it.
                    ["conferenceName"] = conferenceName,
                    ["participantCount"] = callIds.Count,
                }));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while merging Telnyx calls.");

            return TelephonyResult.Failed(S["Telnyx could not merge the calls."].Value);
        }
    }

    // Creates the conference from the primary call and returns its id, or null when Telnyx refused.
    private async Task<string> CreateConferenceFromAsync(string conferenceName, string primaryCallId, CancellationToken cancellationToken)
    {
        var bridge = await FindBridgedDialAsync(primaryCallId, cancellationToken);

        var created = bridge is null
            ? await _apiClient.CreateConferenceAsync(conferenceName, primaryCallId, cancellationToken: cancellationToken)
            : await _apiClient.CreateConferenceWithStateAsync(conferenceName, bridge.RemoteLegId, RemoteLegState(bridge).ToClientStateJson(), cancellationToken);

        if (!created.Succeeded || string.IsNullOrWhiteSpace(created.ConferenceId))
        {
            _logger.LogError(
                "Telnyx rejected a conference creation request with status code {StatusCode}. Response: {Response}",
                created.StatusCode,
                created.ErrorBody.SanitizeLogValue());

            return null;
        }

        if (bridge is null)
        {
            return created.ConferenceId;
        }

        // The agent joins the conference on the leg the bridge parked when the dialed party moved into it.
        var joined = await _apiClient.JoinConferenceWithStateAsync(
            created.ConferenceId,
            bridge.AgentLegId,
            endConferenceOnExit: true,
            clientStateJson: null,
            cancellationToken);

        if (!joined.Succeeded)
        {
            _logger.LogError(
                "Telnyx rejected joining the agent's leg {CallId} to conference '{ConferenceName}' with status code {StatusCode}. Response: {Response}",
                bridge.AgentLegId.SanitizeLogValue(),
                conferenceName.SanitizeLogValue(),
                joined.StatusCode,
                joined.ErrorBody.SanitizeLogValue());

            return null;
        }

        return created.ConferenceId;
    }

    // Digits are played from a leg to the party at its other end, so for a dialed number they are sent from the dialed
    // party's leg: sent from the agent's leg they would be played to the agent's own browser.
    private async Task<TelephonyResult> SendDigitsCoreAsync(SendDigitsRequest request, CancellationToken cancellationToken)
    {
        var bridge = _options.IsConfigured ? await FindBridgedDialAsync(request.CallId, cancellationToken) : null;

        return await ExecuteActionAsync(
            bridge?.RemoteLegId ?? request.CallId,
            "send_dtmf",
            new Dictionary<string, object> { ["digits"] = request.Digits },
            () => null,
            cancellationToken);
    }
}
