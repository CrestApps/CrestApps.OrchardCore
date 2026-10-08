using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Answers an inbound Telnyx call for an AI voice agent. The answer carries the AI voice leg's client state, the same
/// state a call the AI places carries, so Telnyx echoes it on every later event of the call and those events drive the
/// automated voice conversation of the activity instead of Contact Center routing.
/// </summary>
public sealed class TelnyxInboundAIVoiceAnswerer : IInboundAIVoiceAnswerer
{
    private readonly TelnyxApiClient _apiClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxInboundAIVoiceAnswerer"/> class.
    /// </summary>
    /// <param name="apiClient">The Telnyx Call Control client.</param>
    public TelnyxInboundAIVoiceAnswerer(TelnyxApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <inheritdoc/>
    public string ProviderName
        => TelnyxConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public async Task<bool> AnswerAsync(string providerCallId, string activityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId) || string.IsNullOrWhiteSpace(activityId))
        {
            return false;
        }

        var clientState = new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.AiVoiceLegIntent,
            ActivityId = activityId,
        }.ToClientState();

        var result = await _apiClient.AnswerAsync(providerCallId, clientState, cancellationToken);

        return result.Succeeded;
    }
}
