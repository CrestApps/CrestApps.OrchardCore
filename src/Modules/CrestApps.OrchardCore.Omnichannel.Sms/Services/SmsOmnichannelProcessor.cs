using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
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
    private readonly IAIProfileManager _profileManager;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly ICatalog<SubjectFlowSettings> _flowSettingsCatalog;
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
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="campaignCatalog">The campaign catalog.</param>
    /// <param name="flowSettingsCatalog">The subject flow settings catalog.</param>
    /// <param name="channelEndpointCatalog">The channel endpoint catalog.</param>
    /// <param name="smsService">The sms service.</param>
    /// <param name="liquidTemplateManager">The liquid template manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsOmnichannelProcessor(
        IAIChatSessionManager aIChatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        ICatalog<SubjectFlowSettings> flowSettingsCatalog,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointCatalog,
        ISmsService smsService,
        ILiquidTemplateManager liquidTemplateManager,
        IContentManager contentManager,
        IClock clock,
        IStringLocalizer<SmsOmnichannelProcessor> stringLocalizer)
    {
        _aIChatSessionManager = aIChatSessionManager;
        _promptStore = promptStore;
        _profileManager = profileManager;
        _campaignCatalog = campaignCatalog;
        _flowSettingsCatalog = flowSettingsCatalog;
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

        var flowSettings = await FindFlowSettingsAsync(activity.SubjectContentType, cancellationToken)
            ?? throw new InvalidOperationException($"Unable to find subject flow settings for the activity '{activity.ItemId}' and subject '{activity.SubjectContentType}'.");

        var profile = await _profileManager.FindByIdAsync(flowSettings.ProfileId, cancellationToken)
            ?? throw new InvalidOperationException($"Unable to find the AI profile '{flowSettings.ProfileId}' for the activity '{activity.ItemId}'.");

        if (profile.Type != AIProfileType.Chat)
        {
            throw new InvalidOperationException($"The AI profile '{profile.ItemId}' must be a chat profile.");
        }

        var profileMetadata = profile.GetOrCreate<AIProfileMetadata>();
        var initialPromptPattern = profileMetadata.InitialPrompt?.Trim();

        if (string.IsNullOrWhiteSpace(initialPromptPattern))
        {
            throw new InvalidOperationException($"The AI profile '{profile.ItemId}' must have Add initial prompt enabled.");
        }

        var campaign = string.IsNullOrWhiteSpace(activity.CampaignId)
            ? null
            : await _campaignCatalog.FindByIdAsync(activity.CampaignId, cancellationToken);

        if (chatSession is null)
        {
            chatSession = new AIChatSession
            {
                SessionId = UniqueId.GenerateId(),
                ProfileId = profile.ItemId,
                CreatedUtc = _clock.UtcNow,
                LastActivityUtc = _clock.UtcNow,
                Title = S["Automated SMS Activity"],
            };
        }

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        var templateContext = new Dictionary<string, FluidValue>
        {
            ["Activity"] = new ObjectValue(activity),
            ["Contact"] = new ObjectValue(contact),
            ["FlowSettings"] = new ObjectValue(flowSettings),
            ["Profile"] = new ObjectValue(profile),
            ["Session"] = new ObjectValue(chatSession),
        };

        if (campaign is not null)
        {
            templateContext["Campaign"] = new ObjectValue(campaign);
        }

        var initialPrompt = await _liquidTemplateManager.RenderStringAsync(
            initialPromptPattern,
            NullEncoder.Default,
            templateContext);

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

        var smsResult = await _smsService.SendAsync(message, cancellationToken);

        if (smsResult.Succeeded)
        {
            await _promptStore.CreateAsync(new AIChatSessionPrompt
            {
                ItemId = UniqueId.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.Assistant,
                Content = initialPrompt,
            }, cancellationToken);

            chatSession.LastActivityUtc = _clock.UtcNow;

            await _aIChatSessionManager.SaveAsync(chatSession, cancellationToken);

            // Update the activity with the AI session details.
            activity.AISessionId = chatSession.SessionId;
            activity.Status = ActivityStatus.AwaitingCustomerAnswer;

            if (OmnichannelAutomationHelper.HasNoResponseTimeout(flowSettings))
            {
                activity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(
                    flowSettings,
                    _clock.UtcNow);
            }
        }
        else
        {
            throw new InvalidOperationException("Failed to send SMS for an automated activity.");
        }
    }

    private async Task<SubjectFlowSettings> FindFlowSettingsAsync(
        string subjectContentType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subjectContentType))
        {
            return null;
        }

        var flowSettings = await _flowSettingsCatalog.GetAllAsync(cancellationToken);

        return flowSettings.FirstOrDefault(settings =>
            string.Equals(settings.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase));
    }
}
