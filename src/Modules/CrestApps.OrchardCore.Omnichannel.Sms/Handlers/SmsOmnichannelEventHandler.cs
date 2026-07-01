using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Handlers;

internal sealed class SmsOmnichannelEventHandler : IOmnichannelEventHandler
{
    private const string SmsConclusionAnalysisPromptId = "sms-conclusion-analysis";

    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAICompletionService _aICompletionService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionContextBuilder _completionContextBuilder;
    private readonly IAIProfileManager _profileManager;
    private readonly ITemplateService _aiTemplateService;
    private readonly IOmnichannelChannelEndpointManager _channelEndpointsManager;
    private readonly ICatalog<SubjectFlowSettings> _flowSettingsCatalog;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;
    private readonly ISession _session;

    private readonly ISmsService _smsService;

    private readonly IOmnichannelActivityStore _omnichannelActivityStore;
    private readonly DocumentJsonSerializerOptions _jsonSerializerOptions;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsOmnichannelEventHandler"/> class.
    /// </summary>
    /// <param name="chatSessionManager">The chat session manager.</param>
    /// <param name="promptStore">The prompt store.</param>
    /// <param name="aICompletionService">The AI completion service.</param>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="completionContextBuilder">The AI completion context builder.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="aiTemplateService">The ai template service.</param>
    /// <param name="channelEndpointsManager">The channel endpoints manager.</param>
    /// <param name="flowSettingsCatalog">The subject flow settings catalog.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="session">The session.</param>
    /// <param name="smsService">The sms service.</param>
    /// <param name="omnichannelActivityStore">The omnichannel activity store.</param>
    /// <param name="jsonSerializerOptions">The json serializer options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsOmnichannelEventHandler(
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAICompletionService aICompletionService,
        IAIDeploymentManager deploymentManager,
        IAICompletionContextBuilder completionContextBuilder,
        IAIProfileManager profileManager,
        ITemplateService aiTemplateService,
        IOmnichannelChannelEndpointManager channelEndpointsManager,
        ICatalog<SubjectFlowSettings> flowSettingsCatalog,
        IContentManager contentManager,
        IClock clock,
        ISession session,
        ISmsService smsService,
        IOmnichannelActivityStore omnichannelActivityStore,
        IOptions<DocumentJsonSerializerOptions> jsonSerializerOptions,
        ILogger<SmsOmnichannelEventHandler> logger,
        IStringLocalizer<SmsOmnichannelEventHandler> stringLocalizer)
    {
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _aICompletionService = aICompletionService;
        _deploymentManager = deploymentManager;
        _completionContextBuilder = completionContextBuilder;
        _profileManager = profileManager;
        _aiTemplateService = aiTemplateService;
        _channelEndpointsManager = channelEndpointsManager;
        _flowSettingsCatalog = flowSettingsCatalog;
        _contentManager = contentManager;
        _clock = clock;
        _session = session;
        _smsService = smsService;
        _omnichannelActivityStore = omnichannelActivityStore;

        _jsonSerializerOptions = jsonSerializerOptions.Value;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Handles the async.
    /// </summary>
    /// <param name="omnichannelEvent">The omnichannel event.</param>
    public async Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
    {
        if (omnichannelEvent.EventType != OmnichannelConstants.Events.SmsReceived ||
            omnichannelEvent.Message.Channel != OmnichannelConstants.Channels.Sms ||
            !omnichannelEvent.Message.IsInbound)
        {
            return;
        }

        var serviceAddress = omnichannelEvent.Message.ServiceAddress.GetCleanedPhoneNumber();

        var endpoint = await _channelEndpointsManager.GetByServiceAddressAsync(omnichannelEvent.Message.Channel, serviceAddress, cancellationToken);

        if (endpoint is null)
        {
            _logger.LogWarning("No channel endpoint found for incoming SMS message. Channel: {Channel}, Service Address: {ServiceAddress}", omnichannelEvent.Message.Channel.SanitizeLogValue(), omnichannelEvent.Message.ServiceAddress.SanitizeLogValue());

            return;
        }

        var activity = await _omnichannelActivityStore.GetAsync(omnichannelEvent.Message.Channel,
        endpoint.ItemId,
        omnichannelEvent.Message.CustomerAddress,
        ActivityInteractionType.Automated,
        cancellationToken);

        if (activity is null)
        {
            _logger.LogWarning("Unable to link incoming SMS message from a customer to an Activity. Channel: {Channel}, Service Address: {ServiceAddress}, Customer Address: {CustomerAddress}", omnichannelEvent.Message.Channel.SanitizeLogValue(), omnichannelEvent.Message.ServiceAddress.SanitizeLogValue(), omnichannelEvent.Message.CustomerAddress.SanitizeLogValue());

            return;
        }

        // Always set the activity status to AwaitingAgentResponse when a new message is received from the customer to ensure we don't miss responding to them.
        activity.Status = ActivityStatus.AwaitingAgentResponse;

        await _omnichannelActivityStore.UpdateAsync(activity, cancellationToken);

        var flowSettings = await FindFlowSettingsAsync(activity.SubjectContentType, cancellationToken);

        if (OmnichannelSmsComplianceHelper.IsOptOutRequest(omnichannelEvent.Message.Content, flowSettings?.SmsOptOutKeywords))
        {
            await ApplySmsOptOutAsync(activity, cancellationToken);

            return;
        }

        if (flowSettings is null)
        {
            _logger.LogWarning("The subject flow settings for subject '{SubjectContentType}' associated with Activity {ActivityId} were not found. Cannot process incoming SMS message.", activity.SubjectContentType, activity.ItemId);

            return;
        }

        if (string.IsNullOrWhiteSpace(flowSettings.ProfileId))
        {
            _logger.LogWarning("The subject flow settings for subject '{SubjectContentType}' associated with Activity {ActivityId} do not have an AI profile. Cannot process incoming SMS message.", activity.SubjectContentType, activity.ItemId);

            return;
        }

        var profile = await _profileManager.FindByIdAsync(flowSettings.ProfileId, cancellationToken);

        if (profile is null || profile.Type != AIProfileType.Chat)
        {
            _logger.LogWarning("The AI profile '{ProfileId}' associated with Activity {ActivityId} was not found or is not a chat profile. Cannot process incoming SMS message.", flowSettings.ProfileId, activity.ItemId);

            return;
        }

        if (string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            _logger.LogWarning("The linked Activity {ActivityId} does not have an AI Session associated with it. Cannot process incoming SMS message.", activity.ItemId);

            return;
        }

        var chatSession = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (chatSession is null)
        {
            _logger.LogWarning("The AI Chat Session {AISessionId} associated with Activity {ActivityId} was not found. Cannot process incoming SMS message.", activity.AISessionId, activity.ItemId);

            return;
        }

        if (!string.IsNullOrWhiteSpace(chatSession.ProfileId) &&
            !string.Equals(chatSession.ProfileId, profile.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            profile = await _profileManager.FindByIdAsync(chatSession.ProfileId, cancellationToken);

            if (profile is null || profile.Type != AIProfileType.Chat)
            {
                _logger.LogWarning("The AI profile '{ProfileId}' associated with AI Chat Session {AISessionId} was not found or is not a chat profile. Cannot process incoming SMS message.", chatSession.ProfileId, chatSession.SessionId);

                return;
            }
        }

        await _promptStore.CreateAsync(new AIChatSessionPrompt
        {
            ItemId = UniqueId.GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.User,
            Content = omnichannelEvent.Message.Content
        }, cancellationToken);

        string bestChoice = null;

        try
        {
            var prompts = await _promptStore.GetPromptsAsync(chatSession.SessionId);

            var transcript = prompts.Where(x => !x.IsGeneratedPrompt)
                .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

            var context = await _completionContextBuilder.BuildAsync(profile, cancellationToken: cancellationToken);

            context.AdditionalProperties["Session"] = chatSession;

            var deployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Unable to resolve a chat deployment for AI profile '{profile.ItemId}'.");

            var completion = await _aICompletionService.CompleteAsync(deployment, transcript, context, cancellationToken);

            bestChoice = completion?.Messages?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(bestChoice))
            {
                _logger.LogWarning("AI Completion did not return any content for Activity {ActivityId} using AI profile {ProfileId}.", activity.ItemId, profile.ItemId);

                return;
            }
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Completion failed for Activity {ActivityId} using AI profile {ProfileId}.", activity.ItemId, profile.ItemId);

            return;
        }

        try
        {
            if (flowSettings.SmsResponseDelayInSeconds is > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(flowSettings.SmsResponseDelayInSeconds.Value), cancellationToken);
            }

            var result = await _smsService.SendAsync(new SmsMessage
            {
                To = activity.PreferredDestination,
                From = endpoint.Value,
                Body = bestChoice,
            }, cancellationToken);

            if (result.Succeeded)
            {
                await _promptStore.CreateAsync(new AIChatSessionPrompt
                {
                    ItemId = UniqueId.GenerateId(),
                    SessionId = chatSession.SessionId,
                    Role = ChatRole.Assistant,
                    Content = bestChoice,
                }, cancellationToken);

                chatSession.LastActivityUtc = _clock.UtcNow;

                activity.Status = ActivityStatus.AwaitingCustomerAnswer;

                if (OmnichannelAutomationHelper.HasNoResponseTimeout(flowSettings))
                {
                    activity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(
                        flowSettings,
                        _clock.UtcNow);
                }

                await _omnichannelActivityStore.UpdateAsync(activity, cancellationToken);

                ShellScope.AddDeferredTask(async scope =>
                {
                    // In a deferred task, we check the status of the converation and concluded it if needed.
                    // we use deferred task here to ensure that we don't hold current process for a longer running
                    // AI conclusion detection.
                    var store = scope.ServiceProvider.GetRequiredService<IOmnichannelActivityStore>();
                    var actionCatalog = scope.ServiceProvider.GetRequiredService<ISourceCatalog<SubjectAction>>();
                    var dispositionCatalog = scope.ServiceProvider.GetRequiredService<ICatalog<OmnichannelDisposition>>();

                    var clientFactory = scope.ServiceProvider.GetRequiredService<IAIClientFactory>();
                    var deploymentManager = scope.ServiceProvider.GetRequiredService<IAIDeploymentManager>();
                    var completionContextBuilder = scope.ServiceProvider.GetRequiredService<IAICompletionContextBuilder>();

                    var deferredPromptStore = scope.ServiceProvider.GetRequiredService<IAIChatSessionPromptStore>();
                    var allActions = await actionCatalog.GetAllAsync();
                    var subjectDispositionIds = allActions
                        .Where(a => string.Equals(a.SubjectContentType, activity.SubjectContentType, StringComparison.OrdinalIgnoreCase))
                        .Select(a => a.DispositionId)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Distinct()
                        .ToList();
                    var dispositions = await dispositionCatalog.GetAsync(subjectDispositionIds);

                    var conclusionPrompt = await _aiTemplateService.RenderAsync(SmsConclusionAnalysisPromptId);
                    var conclusionContext = await completionContextBuilder.BuildAsync(profile, context =>
                    {
                        context.SystemMessage = conclusionPrompt;
                        context.DisableTools = true;
                    });

                    var deployment = await deploymentManager.ResolveOrDefaultAsync(
                        AIDeploymentPurpose.Chat,
                        deploymentName: conclusionContext.ChatDeploymentName);

                    if (deployment == null)
                    {
                        return;
                    }

                    var client = await clientFactory.CreateChatClientAsync(deployment);

                    var contentManager = scope.ServiceProvider.GetRequiredService<IContentManager>();

                    ContentItem subject = null;
                    ContentItem contact = null;

                    var sessionPrompts = await deferredPromptStore.GetPromptsAsync(chatSession.SessionId);

                    var userPrompt = $"""

                        Chat Summary: {JsonSerializer.Serialize(sessionPrompts)}
                        Subject Goal: {flowSettings.SubjectGoal}
                        List of Dispositions: {JsonSerializer.Serialize(dispositions.Select(x => new { Id = x.ItemId, x.Name, x.Description }))}

                        """;

                    if (flowSettings.AllowAIToUpdateSubject)
                    {
                        subject ??= activity.Subject ?? await contentManager.NewAsync(activity.SubjectContentType);

                        userPrompt +=
                            $"""
                            Subject: {JsonSerializer.Serialize(activity.Subject, _jsonSerializerOptions.SerializerOptions)}
                            """;
                    }

                    if (flowSettings.AllowAIToUpdateContact)
                    {
                        contact ??= await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

                        userPrompt +=

                            $"""

                            Contact: {JsonSerializer.Serialize(contact, _jsonSerializerOptions.SerializerOptions)}
                            """;
                    }

                    var transcript = new List<ChatMessage>
                    {
                        new (ChatRole.System, conclusionPrompt),
                        new (ChatRole.User, userPrompt),
                    };

                    var result = await client.GetResponseAsync<ConverationConclusionResult>(transcript, _jsonSerializerOptions.SerializerOptions);

                    if (result.Result is not null)
                    {
                        OmnichannelActivity omnichannelActivity = null;

                        if (flowSettings.AllowAIToUpdateSubject && result.Result.Subject is not null)
                        {
                            subject ??= activity.Subject ?? await contentManager.NewAsync(activity.SubjectContentType);
                            subject.Merge(result.Result.Subject);

                            omnichannelActivity ??= await store.FindByIdAsync(activity.ItemId);

                            omnichannelActivity.Subject = subject;

                            // Update the activity with the new subject since the converation may not be concluded.
                            await store.UpdateAsync(omnichannelActivity);
                        }

                        if (flowSettings.AllowAIToUpdateContact && result.Result.Contact is not null)
                        {
                            contact ??= await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

                            if (contact is not null)
                            {
                                contact.Merge(result.Result.Contact);

                                await contentManager.UpdateAsync(contact);
                            }
                        }

                        if (result.Result.Concluded)
                        {
                            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                            var executor = scope.ServiceProvider.GetRequiredService<ISubjectActionExecutor>();

                            omnichannelActivity ??= await store.FindByIdAsync(activity.ItemId);

                            omnichannelActivity.Status = ActivityStatus.Completed;

                            omnichannelActivity.CompletedUtc = clock.UtcNow;

                            omnichannelActivity.DispositionId = result.Result.DispositionId;

                            omnichannelActivity.CompletedById = omnichannelActivity.AssignedToId;
                            omnichannelActivity.CompletedByUsername = omnichannelActivity.AssignedToUsername;

                            await store.UpdateAsync(omnichannelActivity);

                            subject ??= activity.Subject ?? await contentManager.NewAsync(activity.SubjectContentType);
                            contact ??= await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

                            var dispositionObj = dispositions.FirstOrDefault(d => d.ItemId == result.Result.DispositionId);

                            await executor.ExecuteAsync(new SubjectActionExecutionContext
                            {
                                Activity = omnichannelActivity,
                                Contact = contact,
                                Subject = subject,
                                Disposition = dispositionObj,
                            });
                        }
                    }
                });

                await _session.SaveAsync(chatSession);
            }
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS message to {To} for Activity {ActivityId}.", activity.PreferredDestination.SanitizeLogValue(), activity.ItemId);
        }

        await _session.SaveAsync(chatSession);
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

    private async Task ApplySmsOptOutAsync(
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        if (contact is null)
        {
            _logger.LogWarning("Unable to update Do not SMS for Activity {ActivityId} because contact {ContactContentItemId} was not found.", activity.ItemId, activity.ContactContentItemId);
        }
        else
        {
            contact.Alter<OmnichannelContactPart>(part =>
            {
                part.SetDoNotSms(true, _clock.UtcNow);
            });

            await _contentManager.UpdateAsync(contact);
        }

        activity.Status = ActivityStatus.Cancelled;

        if (string.IsNullOrWhiteSpace(activity.Notes))
        {
            activity.Notes = "The automated SMS activity was cancelled because the contact requested SMS opt-out.";
        }

        await _omnichannelActivityStore.UpdateAsync(activity, cancellationToken);
    }

    private sealed class ConverationConclusionResult
    {
        /// <summary>
        /// Gets or sets the concluded.
        /// </summary>
        public bool Concluded { get; set; }

        /// <summary>
        /// Gets or sets the disposition id.
        /// </summary>
        public string DispositionId { get; set; }

        /// <summary>
        /// Gets or sets the subject.
        /// </summary>
        public ContentItem Subject { get; set; }

        /// <summary>
        /// Gets or sets the contact.
        /// </summary>
        public ContentItem Contact { get; set; }
    }
}
