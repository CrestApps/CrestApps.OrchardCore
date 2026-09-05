using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The Phone-channel omnichannel processor. It originates an outbound Telnyx call handled by the automated AI
/// voice agent, tagging the leg's client_state with the activity so the webhook conversation loop can drive it.
/// </summary>
public sealed class VoiceOmnichannelProcessor : IOmnichannelProcessor
{
    private readonly ITelnyxVoiceAgentClient _voiceClient;
    private readonly ICatalog<OmnichannelChannelEndpoint> _channelEndpointCatalog;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public VoiceOmnichannelProcessor(
        ITelnyxVoiceAgentClient voiceClient,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointCatalog,
        IAIChatSessionManager chatSessionManager,
        IClock clock,
        ILogger<VoiceOmnichannelProcessor> logger)
    {
        _voiceClient = voiceClient;
        _channelEndpointCatalog = channelEndpointCatalog;
        _chatSessionManager = chatSessionManager;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Channel { get; } = OmnichannelConstants.Channels.Phone;

    /// <inheritdoc/>
    public async Task StartAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity.PreferredDestination))
        {
            throw new InvalidOperationException($"The automated voice activity '{activity.ItemId}' has no destination to dial.");
        }

        string from = null;

        if (!string.IsNullOrWhiteSpace(activity.ChannelEndpointId))
        {
            var endpoint = await _channelEndpointCatalog.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

            if (endpoint is not null && string.Equals(endpoint.Channel, activity.Channel, StringComparison.OrdinalIgnoreCase))
            {
                from = endpoint.Value;
            }
        }

        // The empty AI session the conversation loop appends turns to. The greeting is spoken when the call is
        // answered, so nothing is generated here.
        if (string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            var session = new AIChatSession
            {
                SessionId = UniqueId.GenerateId(),
                ProfileId = activity.AIProfileId,
                CreatedUtc = _clock.UtcNow,
                LastActivityUtc = _clock.UtcNow,
                Title = "Automated AI Voice Call",
            };

            await _chatSessionManager.SaveAsync(session, cancellationToken);
            activity.AISessionId = session.SessionId;
        }

        var callControlId = await _voiceClient.OriginateAsync(
            to: activity.PreferredDestination,
            from: from,
            clientState: new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.AiVoiceLegIntent,
                ActivityId = activity.ItemId,
            },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(callControlId))
        {
            throw new InvalidOperationException($"Failed to originate the automated voice call for activity '{activity.ItemId}'.");
        }

        activity.Status = ActivityStatus.AwaitingCustomerAnswer;

        _logger.LogInformation("Originated automated AI voice call for activity '{ActivityId}' to the destination.", activity.ItemId.SanitizeLogValue());
    }
}
