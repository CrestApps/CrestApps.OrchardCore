using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.Core.Support;
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
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly ICatalog<OmnichannelChannelEndpoint> _channelEndpointCatalog;
    private readonly ISmsService _smsService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IContentManager _contentManager;
    private readonly TimeProvider _timeProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsOmnichannelProcessor"/> class.
    /// </summary>
    /// <param name="aIChatSessionManager">The AI chat session manager.</param>
    /// <param name="promptStore">The prompt store.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="campaignCatalog">The campaign catalog.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="channelEndpointCatalog">The channel endpoint catalog.</param>
    /// <param name="smsService">The sms service.</param>
    /// <param name="liquidTemplateManager">The liquid template manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsOmnichannelProcessor(
        IAIChatSessionManager aIChatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointCatalog,
        ISmsService smsService,
        ILiquidTemplateManager liquidTemplateManager,
        IContentManager contentManager,
        TimeProvider timeProvider,
        IStringLocalizer<SmsOmnichannelProcessor> stringLocalizer)
    {
        _aIChatSessionManager = aIChatSessionManager;
        _promptStore = promptStore;
        _profileManager = profileManager;
        _campaignCatalog = campaignCatalog;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _channelEndpointCatalog = channelEndpointCatalog;
        _smsService = smsService;
        _liquidTemplateManager = liquidTemplateManager;
        _contentManager = contentManager;
        _timeProvider = timeProvider;
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

        var profileId = string.IsNullOrWhiteSpace(activity.AIProfileId)
            ? flowSettings.ProfileId
            : activity.AIProfileId;

        var profile = await _profileManager.FindByIdAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException($"Unable to find the AI profile '{profileId}' for the activity '{activity.ItemId}'.");

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
                CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime,
                LastActivityUtc = _timeProvider.GetUtcNow().UtcDateTime,
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

        // Asked here as well as in the pass that schedules this, because this is not the only way in: the
        // "Place Call or Send Message" workflow task calls StartAsync directly and screens nothing, and a retry
        // re-enters here too. This method is the last code before the carrier and it has already loaded the
        // contact for the template, so the question costs nothing and asking it is what makes the guarantee hold
        // for every caller rather than for one of them.
        if (OmnichannelContactPreferences.HasOptedOut(ContentItemOmnichannelContactProjection.Project(contact), activity.Channel))
        {
            return;
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
                // Stamp the opening message so it orders before the customer's first reply. Without a time it
                // defaults to DateTime.MinValue and sorts ahead of every later message, corrupting the owed-reply
                // scan and the transcript for the rest of the conversation.
                CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime,
            }, cancellationToken);

            chatSession.LastActivityUtc = _timeProvider.GetUtcNow().UtcDateTime;

            await _aIChatSessionManager.SaveAsync(chatSession, cancellationToken);

            // Update the activity with the AI session details.
            activity.AISessionId = chatSession.SessionId;
            activity.Status = ActivityStatus.AwaitingCustomerAnswer;

            if (OmnichannelAutomationHelper.HasNoResponseTimeout(flowSettings))
            {
                activity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(
                    flowSettings,
                    _timeProvider.GetUtcNow().UtcDateTime);
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

        return await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(subjectContentType, cancellationToken);
    }
}
