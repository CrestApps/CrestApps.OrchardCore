using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// Represents the sms omnichannel processor.
/// </summary>
public sealed class SmsOmnichannelProcessor : IOmnichannelProcessor
{
    private readonly IAIChatSessionManager _aIChatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly ICatalog<OmnichannelChannelEndpoint> _channelEndpointCatalog;
    private readonly ISmsService _smsService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsOmnichannelProcessor"/> class.
    /// </summary>
    /// <param name="aIChatSessionManager">The AI chat session manager.</param>
    /// <param name="promptStore">The prompt store.</param>
    /// <param name="campaignCatalog">The campaign catalog.</param>
    /// <param name="channelEndpointCatalog">The channel endpoint catalog.</param>
    /// <param name="smsService">The sms service.</param>
    /// <param name="liquidTemplateManager">The liquid template manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsOmnichannelProcessor(
        IAIChatSessionManager aIChatSessionManager,
        IAIChatSessionPromptStore promptStore,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointCatalog,
        ISmsService smsService,
        ILiquidTemplateManager liquidTemplateManager,
        IContentManager contentManager,
        IClock clock,
        IStringLocalizer<SmsOmnichannelProcessor> stringLocalizer)
    {
        _aIChatSessionManager = aIChatSessionManager;
        _promptStore = promptStore;
        _campaignCatalog = campaignCatalog;
        _channelEndpointCatalog = channelEndpointCatalog;
        _smsService = smsService;
        _liquidTemplateManager = liquidTemplateManager;
        _contentManager = contentManager;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the channel.
    /// </summary>
    public string Channel { get; } = OmnichannelConstants.Channels.Sms;

    /// <summary>
    /// Asynchronously performs the start operation.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StartAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        AIChatSession chatSession = null;

        if (!string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            chatSession = await _aIChatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);
        }

        var campaign = await _campaignCatalog.FindByIdAsync(activity.CampaignId, cancellationToken)
        ?? throw new InvalidOperationException($"Unable to find the campaign '{activity.CampaignId}' that is associated with the activity '{activity.ItemId}'.");

        if (chatSession is null)
        {
            chatSession = new AIChatSession
            {
                SessionId = UniqueId.GenerateId(),
                CreatedUtc = _clock.UtcNow,
                Title = S["Automated SMS Activity"],
            };

            await _promptStore.CreateAsync(new AIChatSessionPrompt
            {
                ItemId = UniqueId.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.System,
                Content = campaign.SystemMessage,
            }, cancellationToken);
        }

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        var initialPrompt = await _liquidTemplateManager.RenderStringAsync(campaign.InitialOutboundPromptPattern, NullEncoder.Default,
        new Dictionary<string, FluidValue>()
        {
            ["Contact"] = new ObjectValue(contact),
            ["Campaign"] = new ObjectValue(campaign),
            ["Session"] = new ObjectValue(chatSession),
        });

        initialPrompt = initialPrompt?.Trim();

        if (string.IsNullOrEmpty(initialPrompt))
        {
            throw new InvalidOperationException("The initial generated prompt is empty.");
        }

        var message = new SmsMessage
        {
            To = activity.PreferredDestination,
            Body = initialPrompt,
        };

        if (!string.IsNullOrEmpty(activity.ChannelEndpointId))
        {
            var endpoint = await _channelEndpointCatalog.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

            if (endpoint is not null && endpoint.Channel == activity.Channel)
            {
                message.From = endpoint.Value;
            }
        }

        var smsResult = await _smsService.SendAsync(message);

        if (smsResult.Succeeded)
        {
            await _promptStore.CreateAsync(new AIChatSessionPrompt
            {
                ItemId = UniqueId.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.Assistant,
                Content = initialPrompt,
            }, cancellationToken);

            await _aIChatSessionManager.SaveAsync(chatSession, cancellationToken);

            // Update the activity with the AI session details.
            activity.AISessionId = chatSession.SessionId;
            activity.Status = ActivityStatus.AwaitingCustomerAnswer;
        }
        else
        {
            throw new InvalidOperationException("Failed to send SMS for an automated activity.");
        }
    }
}
