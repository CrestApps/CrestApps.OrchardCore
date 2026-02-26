using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

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

    public string Channel { get; } = OmnichannelConstants.Channels.Sms;

    public async Task StartAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        AIChatSession chatSession = null;

        if (!string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            chatSession = await _aIChatSessionManager.FindByIdAsync(activity.AISessionId);
        }

        var campaign = await _campaignCatalog.FindByIdAsync(activity.CampaignId)
            ?? throw new InvalidOperationException($"Unable to find the campaign '{activity.CampaignId}' that is associated with the activity '{activity.ItemId}'.");

        if (chatSession is null)
        {
            chatSession = new AIChatSession
            {
                SessionId = IdGenerator.GenerateId(),
                CreatedUtc = _clock.UtcNow,
                Title = S["Automated SMS Activity"],
            };

            await _promptStore.CreateAsync(new AIChatSessionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.System,
                Content = campaign.SystemMessage,
            });
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
            var endpoint = await _channelEndpointCatalog.FindByIdAsync(activity.ChannelEndpointId);

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
                ItemId = IdGenerator.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.Assistant,
                Content = initialPrompt,
            });

            await _aIChatSessionManager.SaveAsync(chatSession);

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
