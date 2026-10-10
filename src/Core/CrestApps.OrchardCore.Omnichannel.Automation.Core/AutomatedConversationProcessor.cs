using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Starts an automated (AI) activity on one channel: renders the AI profile's opening message for the contact, sends it
/// from the activity's address, and opens the conversation the replies continue in. The automated activities task picks
/// the processor by the activity's channel, so one instance serves one channel.
/// </summary>
public sealed class AutomatedConversationProcessor : IOmnichannelProcessor
{
    private readonly IAutomatedMessagingChannel _automatedChannel;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIProfileManager _profileManager;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IContentManager _contentManager;
    private readonly IContactOptOutResolver _optOutResolver;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomatedConversationProcessor"/> class.
    /// </summary>
    /// <param name="automatedChannel">The channel this processor starts activities on.</param>
    /// <param name="channelResolver">The resolver of the messaging channel the opening message is sent through.</param>
    /// <param name="chatSessionManager">The AI chat session manager.</param>
    /// <param name="promptStore">The AI chat transcript store.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="campaignCatalog">The campaign catalog.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="endpointManager">The manager of the business's addresses.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager the opening message is rendered with.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="optOutResolver">The resolver of a contact's opt-outs.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AutomatedConversationProcessor(
        IAutomatedMessagingChannel automatedChannel,
        IMessagingChannelResolver channelResolver,
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IOmnichannelChannelEndpointManager endpointManager,
        ILiquidTemplateManager liquidTemplateManager,
        IContentManager contentManager,
        IContactOptOutResolver optOutResolver,
        IClock clock,
        ILogger<AutomatedConversationProcessor> logger)
    {
        _automatedChannel = automatedChannel;
        _channelResolver = channelResolver;
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _profileManager = profileManager;
        _campaignCatalog = campaignCatalog;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _endpointManager = endpointManager;
        _liquidTemplateManager = liquidTemplateManager;
        _contentManager = contentManager;
        _optOutResolver = optOutResolver;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Channel => _automatedChannel.Channel;

    /// <inheritdoc/>
    public async Task StartAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var messagingChannel = _channelResolver.Get(_automatedChannel.Channel)
            ?? throw new InvalidOperationException($"The '{_automatedChannel.Channel}' messaging channel is not enabled.");

        var flowSettings = (string.IsNullOrWhiteSpace(activity.SubjectContentType)
            ? null
            : await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(activity.SubjectContentType, cancellationToken))
            ?? throw new InvalidOperationException($"Unable to find subject flow settings for the activity '{activity.ItemId}' and subject '{activity.SubjectContentType}'.");

        var profileId = string.IsNullOrWhiteSpace(activity.AIProfileId) ? flowSettings.ProfileId : activity.AIProfileId;
        var profile = await _profileManager.FindByIdAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException($"Unable to find the AI profile '{profileId}' for the activity '{activity.ItemId}'.");

        if (profile.Type != AIProfileType.Chat)
        {
            throw new InvalidOperationException($"The AI profile '{profile.ItemId}' must be a chat profile.");
        }

        var openingPattern = profile.GetOrCreate<AIProfileMetadata>().InitialPrompt?.Trim();

        if (string.IsNullOrWhiteSpace(openingPattern))
        {
            throw new InvalidOperationException($"The AI profile '{profile.ItemId}' must have Start the conversation automatically enabled, with an opening message.");
        }

        var endpoint = await ResolveSendingAddressAsync(activity, flowSettings, cancellationToken)
            ?? throw new InvalidOperationException($"The activity '{activity.ItemId}' has no {_automatedChannel.Channel} address to send its opening message from. Pick one on the activity load or on the subject.");

        var chatSession = string.IsNullOrWhiteSpace(activity.AISessionId)
            ? null
            : await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        chatSession ??= new AIChatSession
        {
            SessionId = UniqueId.GenerateId(),
            ProfileId = profile.ItemId,
            CreatedUtc = _clock.UtcNow,
            LastActivityUtc = _clock.UtcNow,
            Title = $"Automated {_automatedChannel.ConversationNoun}",
        };

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        // Asked here as well as in the pass that scheduled it, because a workflow task and a retry reach this method too,
        // and this is the last code before the provider. A person who asked not to be contacted at this address on any
        // record is the same person on this one.
        if (await _optOutResolver.HasOptedOutAsync(contact, activity.Channel, cancellationToken))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("The contact of automated Activity {ActivityId} opted out of {Channel}; no opening message was sent.", activity.ItemId.SanitizeLogValue(), activity.Channel.SanitizeLogValue());
            }

            return;
        }

        var rendered = await RenderOpeningMessageAsync(openingPattern, activity, contact, flowSettings, profile, chatSession, cancellationToken);
        var (subject, body) = _automatedChannel.SplitOpeningMessage(rendered);

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("The rendered opening message is empty.");
        }

        var result = await messagingChannel.SendAsync(new MessagingOutboundMessage
        {
            ServiceAddress = endpoint.Value,
            ContactAddress = activity.PreferredDestination,
            Purpose = MessagingOutboundPurpose.Outreach,
            Subject = subject,
            Body = body,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"The {_automatedChannel.Channel} opening message of the activity '{activity.ItemId}' was not sent: {result.GetErrorText()}");
        }

        // The opening message is the first assistant turn, stamped so it orders before the customer's first reply.
        await _promptStore.CreateAsync(new AIChatSessionPrompt
        {
            ItemId = UniqueId.GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.Assistant,
            Content = body,
            CreatedUtc = _clock.UtcNow,
        }, cancellationToken);

        chatSession.LastActivityUtc = _clock.UtcNow;

        await _chatSessionManager.SaveAsync(chatSession, cancellationToken);

        activity.AISessionId = chatSession.SessionId;
        activity.ChannelEndpointId ??= endpoint.ItemId;
        activity.Put(new AutomatedConversationThread
        {
            Subject = subject,
            OpeningMessageId = result.ProviderMessageId,
        });
        activity.Status = ActivityStatus.AwaitingCustomerAnswer;

        if (OmnichannelAutomationHelper.HasNoResponseTimeout(flowSettings))
        {
            activity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(flowSettings, _clock.UtcNow);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Sent the {Channel} opening message of automated Activity {ActivityId}.", activity.Channel.SanitizeLogValue(), activity.ItemId.SanitizeLogValue());
        }
    }

    // The address the activity was loaded with, else the one its subject answers on, as long as it is used on the channel.
    private async Task<OmnichannelChannelEndpoint> ResolveSendingAddressAsync(OmnichannelActivity activity, SubjectFlowSettings flowSettings, CancellationToken cancellationToken)
    {
        foreach (var id in new[] { activity.ChannelEndpointId, flowSettings.ChannelEndpointId })
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var endpoint = await _endpointManager.FindByIdAsync(id, cancellationToken);

            if (endpoint is not null && endpoint.HasCapability(_automatedChannel.Channel) && !string.IsNullOrWhiteSpace(endpoint.Value))
            {
                return endpoint;
            }
        }

        return null;
    }

    private async Task<string> RenderOpeningMessageAsync(
        string pattern,
        OmnichannelActivity activity,
        ContentItem contact,
        SubjectFlowSettings flowSettings,
        AIProfile profile,
        AIChatSession chatSession,
        CancellationToken cancellationToken)
    {
        var templateContext = new Dictionary<string, FluidValue>
        {
            ["Activity"] = new ObjectValue(activity),
            ["Contact"] = new ObjectValue(contact),
            ["FlowSettings"] = new ObjectValue(flowSettings),
            ["Profile"] = new ObjectValue(profile),
            ["Session"] = new ObjectValue(chatSession),
        };

        if (!string.IsNullOrWhiteSpace(activity.CampaignId) &&
            await _campaignCatalog.FindByIdAsync(activity.CampaignId, cancellationToken) is { } campaign)
        {
            templateContext["Campaign"] = new ObjectValue(campaign);
        }

        var rendered = await _liquidTemplateManager.RenderStringAsync(pattern, NullEncoder.Default, templateContext);

        return rendered?.Trim();
    }
}
