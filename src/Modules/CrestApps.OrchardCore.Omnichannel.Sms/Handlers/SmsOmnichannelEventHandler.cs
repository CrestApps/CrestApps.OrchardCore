using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;
using OrchardCore.Flows.Models;
using OrchardCore.Locking;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Handlers;

internal sealed class SmsOmnichannelEventHandler : IOmnichannelEventHandler
{
    private const string SmsConclusionAnalysisPromptId = "sms-conclusion-analysis";

    // The shared AI profile prompt appends this control marker when it decides the conversation is over (the voice
    // channel uses it to hang up). It must be stripped from the customer-facing SMS, and it doubles as a reliable
    // signal to conclude the activity.
    private const string HangupMarker = "[[HANGUP]]";

    // Before each reply the agent judges whether a reply is actually warranted, the way a person would glance at the
    // thread and sometimes send nothing. This keeps the AI from parroting a message that repeats what was already
    // covered, acknowledging a bare "ok"/"thanks", or reviving a conversation that has naturally wound down.
    private const string ShouldRespondPrompt =
        """
        You are the sales agent in an ongoing SMS conversation with a customer. Read the whole conversation, then
        decide whether the agent should send a NEW reply to the customer's most recent message right now.
        Set ShouldRespond to true whenever the customer answered a question you asked or gave new information —
        including a short confirmation such as "yes" or "no" in response to your question — because a person would
        acknowledge it and then either continue or gracefully close the conversation.
        Set ShouldRespond to false only when a thoughtful human agent would genuinely send nothing: the latest message
        is a bare acknowledgement that needs no follow-up (such as "ok" or "thanks") after you have already wrapped up;
        or it merely repeats a point you already asked about or answered and adds no new information; or the
        conversation has clearly ended. When you are unsure, prefer to respond. Always include a short Reason.
        """;

    // One AI response is generated per conversation at a time. Each in-flight response registers its cancellation
    // source here, keyed by session id; when a newer message arrives it cancels the running one (the conversation
    // changed, so that response is stale) and registers its own. Static so it is shared across the scoped handler
    // instances that separate inbound webhooks create.
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _activeGenerations = new(StringComparer.Ordinal);

    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAICompletionService _aICompletionService;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionContextBuilder _completionContextBuilder;
    private readonly IAIProfileManager _profileManager;
    private readonly ITemplateService _aiTemplateService;
    private readonly IOmnichannelChannelEndpointManager _channelEndpointsManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;
    private readonly ISession _session;

    private readonly ISmsService _smsService;

    private readonly IOmnichannelActivityStore _omnichannelActivityStore;
    private readonly IEnumerable<IOmnichannelHandoffService> _handoffServices;
    private readonly ILocalLock _localLock;
    private readonly DocumentJsonSerializerOptions _jsonSerializerOptions;
    private readonly Redactor _addressRedactor;
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
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="session">The session.</param>
    /// <param name="smsService">The sms service.</param>
    /// <param name="omnichannelActivityStore">The omnichannel activity store.</param>
    /// <param name="jsonSerializerOptions">The json serializer options.</param>
    /// <param name="redactorProvider">The redactor provider used to redact sensitive values before logging.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsOmnichannelEventHandler(
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAICompletionService aICompletionService,
        IAIClientFactory aiClientFactory,
        IAIDeploymentManager deploymentManager,
        IAICompletionContextBuilder completionContextBuilder,
        IAIProfileManager profileManager,
        ITemplateService aiTemplateService,
        IOmnichannelChannelEndpointManager channelEndpointsManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IContentManager contentManager,
        IClock clock,
        ISession session,
        ISmsService smsService,
        IOmnichannelActivityStore omnichannelActivityStore,
        IEnumerable<IOmnichannelHandoffService> handoffServices,
        ILocalLock localLock,
        IOptions<DocumentJsonSerializerOptions> jsonSerializerOptions,
        IRedactorProvider redactorProvider,
        ILogger<SmsOmnichannelEventHandler> logger,
        IStringLocalizer<SmsOmnichannelEventHandler> stringLocalizer)
    {
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _aICompletionService = aICompletionService;
        _aiClientFactory = aiClientFactory;
        _deploymentManager = deploymentManager;
        _completionContextBuilder = completionContextBuilder;
        _profileManager = profileManager;
        _aiTemplateService = aiTemplateService;
        _channelEndpointsManager = channelEndpointsManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _contentManager = contentManager;
        _clock = clock;
        _session = session;
        _smsService = smsService;
        _omnichannelActivityStore = omnichannelActivityStore;
        _handoffServices = handoffServices;
        _localLock = localLock;

        _jsonSerializerOptions = jsonSerializerOptions.Value;
        _addressRedactor = redactorProvider.GetRedactor(LogDataClassifications.AddressSet);
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
            _logger.LogWarning("No channel endpoint found for incoming SMS message. Channel: {Channel}, Service Address: {ServiceAddress}", omnichannelEvent.Message.Channel.SanitizeLogValue(), _addressRedactor.Redact(omnichannelEvent.Message.ServiceAddress));

            return;
        }

        var activity = await _omnichannelActivityStore.GetAsync(omnichannelEvent.Message.Channel,
        endpoint.ItemId,
        omnichannelEvent.Message.CustomerAddress,
        ActivityInteractionType.Automated,
        cancellationToken);

        if (activity is null)
        {
            _logger.LogWarning("Unable to link incoming SMS message from a customer to an Activity. Channel: {Channel}, Service Address: {ServiceAddress}, Customer Address: {CustomerAddress}", omnichannelEvent.Message.Channel.SanitizeLogValue(), _addressRedactor.Redact(omnichannelEvent.Message.ServiceAddress), _addressRedactor.Redact(omnichannelEvent.Message.CustomerAddress));

            return;
        }

        // The conversation has a natural end. Once an automated activity has concluded (Completed), been cancelled
        // (opt-out), failed (no-response timeout / exhausted attempts), or purged, it must not be reopened: a later
        // "thanks" or "ok" from the customer is acknowledged as history but does not resurrect the AI and start
        // replying again. GetAsync returns the latest activity for this number regardless of status, so this guard is
        // what stops a closed conversation from answering on and on after it has already ended.
        if (activity.Status is ActivityStatus.Completed
            or ActivityStatus.Cancelled
            or ActivityStatus.Failed
            or ActivityStatus.Purged)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Ignoring an inbound SMS for Activity {ActivityId} because the automated conversation has already ended with status {Status}.", activity.ItemId.SanitizeLogValue(), activity.Status);
            }

            return;
        }

        var flowSettings = await FindFlowSettingsAsync(activity.SubjectContentType, cancellationToken);

        if (OmnichannelSmsComplianceHelper.IsOptOutRequest(omnichannelEvent.Message.Content, flowSettings?.SmsOptOutKeywords))
        {
            await ApplySmsOptOutAsync(activity, cancellationToken);

            return;
        }

        if (flowSettings is null)
        {
            _logger.LogWarning("The subject flow settings for subject '{SubjectContentType}' associated with Activity {ActivityId} were not found. Cannot process incoming SMS message.", activity.SubjectContentType, activity.ItemId.SanitizeLogValue());

            return;
        }

        var profileId = string.IsNullOrWhiteSpace(activity.AIProfileId)
            ? flowSettings.ProfileId
            : activity.AIProfileId;

        if (string.IsNullOrWhiteSpace(profileId))
        {
            _logger.LogWarning("The subject flow settings for subject '{SubjectContentType}' associated with Activity {ActivityId} do not have an AI profile. Cannot process incoming SMS message.", activity.SubjectContentType, activity.ItemId.SanitizeLogValue());

            return;
        }

        var profile = await _profileManager.FindByIdAsync(profileId, cancellationToken);

        if (profile is null || profile.Type != AIProfileType.Chat)
        {
            _logger.LogWarning("The AI profile '{ProfileId}' associated with Activity {ActivityId} was not found or is not a chat profile. Cannot process incoming SMS message.", profileId.SanitizeLogValue(), activity.ItemId.SanitizeLogValue());

            return;
        }

        if (string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            _logger.LogWarning("The linked Activity {ActivityId} does not have an AI Session associated with it. Cannot process incoming SMS message.", activity.ItemId.SanitizeLogValue());

            return;
        }

        var chatSession = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (chatSession is null)
        {
            _logger.LogWarning("The AI Chat Session {AISessionId} associated with Activity {ActivityId} was not found. Cannot process incoming SMS message.", activity.AISessionId.SanitizeLogValue(), activity.ItemId.SanitizeLogValue());

            return;
        }

        if (!string.IsNullOrWhiteSpace(chatSession.ProfileId) &&
            !string.Equals(chatSession.ProfileId, profile.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            profile = await _profileManager.FindByIdAsync(chatSession.ProfileId, cancellationToken);

            if (profile is null || profile.Type != AIProfileType.Chat)
            {
                _logger.LogWarning("The AI profile '{ProfileId}' associated with AI Chat Session {AISessionId} was not found or is not a chat profile. Cannot process incoming SMS message.", chatSession.ProfileId.SanitizeLogValue(), chatSession.SessionId.SanitizeLogValue());

                return;
            }
        }

        // Tag every log line for this turn with the conversation's identifiers so a single customer's exchange can be
        // followed end to end when many conversations are interleaved in the log (this also enriches the webhook's
        // MessageSid scope, and adds the correlation for the recovery path, which does not go through the webhook).
        using var conversationLogScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ActivityId"] = activity.ItemId,
            ["SessionId"] = chatSession.SessionId,
        });

        // Store the inbound customer message, unless it is already the trailing message in the transcript. This makes
        // the handler idempotent so it can be re-driven safely: a Twilio re-delivery of the same message, or the
        // owed-reply recovery task re-driving a conversation whose in-memory generation was lost on a restart, must
        // not append a duplicate customer turn that the model would then read twice.
        var existingPrompts = (await _promptStore.GetPromptsAsync(chatSession.SessionId))
            .Where(x => !x.IsGeneratedPrompt)
            .ToList();

        var lastPrompt = existingPrompts.LastOrDefault();
        var alreadyStored = lastPrompt is not null
            && lastPrompt.Role == ChatRole.User
            && string.Equals(lastPrompt.Content?.Trim(), omnichannelEvent.Message.Content?.Trim(), StringComparison.Ordinal);

        if (!alreadyStored)
        {
            await _promptStore.CreateAsync(new AIChatSessionPrompt
            {
                ItemId = UniqueId.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.User,
                Content = omnichannelEvent.Message.Content,
                CreatedUtc = _clock.UtcNow,
            }, cancellationToken);
        }

        // One AI response per conversation at a time. Register this turn as the active generation, cancelling any
        // response that is still being composed for this conversation — the customer's new message makes it stale.
        // A later message will cancel us in turn. `generationToken` threads that cancellation through the settle,
        // the should-respond check, the AI call and the "typing" pause, so a superseded turn stops promptly and only
        // the newest turn goes on to send. The lock below still serializes the send itself so two responses can never
        // be dispatched at once. (ILocalLock is a real in-process lock; IDistributedLock is a no-op here — no Redis.)
        using var generation = RegisterGeneration(chatSession.SessionId, cancellationToken);
        var generationToken = generation.Token;

        var hangupRequested = false;

        // A turn is "handled" once we replied or the agent deliberately chose not to; both advance the activity and
        // run the conclusion check, so a skipped reply never strands a naturally-ended thread.
        var handledTurn = false;

        // Resolve the SMS handoff destination once for this turn. Handoff is only offered to the model, and only
        // honored, when the flow both enables it (with a target queue) and a channel implementation is registered
        // (the SMS Workspace feature). This prevents the model from promising a human when there is nowhere to route.
        var smsHandoffService = _handoffServices?.FirstOrDefault(service => service.CanHandle(OmnichannelConstants.Channels.Sms));
        var handoffAvailable = smsHandoffService is not null && OmnichannelHandoffHelper.IsHandoffEnabled(flowSettings);
        var handoffRequested = false;
        string handoffReason = null;

        try
        {
            var (locker, locked) = await _localLock.TryAcquireLockAsync(
                $"SMS_CONVERSATION_{chatSession.SessionId}",
                TimeSpan.FromSeconds(90),
                TimeSpan.FromMinutes(2));

            if (!locked)
            {
                _logger.LogWarning("Timed out waiting for the SMS conversation lock for Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());

                return;
            }

            await using (locker)
            {
                // The reply-pacing delay chosen when the inventory was loaded is snapshotted on the activity; fall back to
                // the subject flow's SMS delay only when the load did not configure one. It is the floor for the humanized
                // settle window below.
                var configuredDelay = OmnichannelAutomationHelper.ResolveResponseDelay(
                    activity.ResponseDelayMode,
                    activity.ResponseDelaySeconds,
                    activity.ResponseDelayJitterSeconds);

                if (configuredDelay is null && flowSettings.SmsResponseDelayInSeconds is > 0)
                {
                    configuredDelay = TimeSpan.FromSeconds(flowSettings.SmsResponseDelayInSeconds.Value);
                }

                while (true)
                {
                    // Only the customer messages received since our last reply are owed an answer. If a prior lock owner
                    // (or an earlier pass of this loop) already answered everything, nothing is pending and we stop
                    // without sending — this is what prevents replying again after the conversation naturally wound down,
                    // and what keeps concurrent handlers from producing a second, duplicate reply.
                    var conversation = (await _promptStore.GetPromptsAsync(chatSession.SessionId))
                        .Where(x => !x.IsGeneratedPrompt)
                        .ToList();

                    var pendingInbound = GetTrailingUserMessages(conversation);

                    if (pendingInbound.Count == 0)
                    {
                        break;
                    }

                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Composing an automated SMS reply; {PendingCount} customer message(s) owed a response.", pendingInbound.Count);
                    }

                    // Humanized settle: a person reads the incoming text and thinks before replying, so wait at least a
                    // few seconds (never instant), longer the more the customer wrote, and answer several quick texts
                    // together. The AI call itself adds more time when it has to research, which reads as natural.
                    var readingDelay = OmnichannelAutomationHelper.ResolveHumanizedReadingDelay(
                        configuredDelay,
                        pendingInbound.Sum(p => p.Content?.Length ?? 0));

                    if (readingDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(readingDelay, generationToken);
                    }

                    // Re-read after settling so any texts that landed during the wait are part of this single reply.
                    var promptsForReply = (await _promptStore.GetPromptsAsync(chatSession.SessionId))
                        .Where(x => !x.IsGeneratedPrompt)
                        .ToList();

                    var transcript = promptsForReply
                        .Select(prompt => new ChatMessage(prompt.Role, prompt.Content))
                        .ToList();

                    // Let the agent decide whether a reply is warranted at all. When it judges that a human would send
                    // nothing (a bare acknowledgement, a repeat of what was already covered, or a wound-down thread), stop
                    // this turn without replying instead of parroting another message.
                    if (!await ShouldRespondAsync(profile, transcript, activity, generationToken))
                    {
                        // The agent chose not to reply this turn. The turn is still handled: below we advance the activity
                        // and run the conclusion check, which closes the conversation if it has naturally ended.
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation("The agent chose not to reply to the current SMS turn; advancing without sending.");
                        }

                        handledTurn = true;

                        break;
                    }

                    string bestChoice;

                    try
                    {
                        var context = await _completionContextBuilder.BuildAsync(profile, cancellationToken: generationToken);
                        context.AdditionalProperties["Session"] = chatSession;

                        // When a live agent is available, enable the transfer tool for this turn and guide the model
                        // on when to escalate. Guidance is injected as a leading system message so it never leaks into
                        // the separate should-respond evaluation above, which runs on the un-augmented transcript.
                        if (handoffAvailable)
                        {
                            AttachTransferToAgentTool(context);

                            var handoffInstructions = OmnichannelHandoffHelper.BuildHandoffInstructions(flowSettings);

                            if (!string.IsNullOrEmpty(handoffInstructions))
                            {
                                transcript.Insert(0, new ChatMessage(ChatRole.System, handoffInstructions));
                            }
                        }

                        var deployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: generationToken)
                            ?? throw new InvalidOperationException($"Unable to resolve a chat deployment for AI profile '{profile.ItemId}'.");

                        // One AI request per turn, cancelled by generationToken the moment a newer inbound message arrives.
                        // A cancelled request unwinds the whole turn so the newer message regenerates the single reply
                        // against the full history, rather than this stale request answering a superseded transcript.
                        // The completion auto-invokes the transfer tool when the model decides to escalate; the tool
                        // records the decision on the ambient turn, which we read back once the completion returns.
                        using var handoffTurn = OmnichannelHandoffTurnContext.Begin();

                        var completion = await _aICompletionService.CompleteAsync(deployment, transcript, context, generationToken);

                        bestChoice = completion?.Messages?.FirstOrDefault()?.Text;
                        handoffRequested = handoffAvailable && handoffTurn.Turn.HandoffRequested;

                        if (handoffRequested)
                        {
                            handoffReason = handoffTurn.Turn.Reason;
                        }
                    }
                    catch (Exception ex) when (generationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        // A newer inbound message superseded this turn mid-generation. The cancellation can surface
                        // wrapped — the AI client turns the aborted HTTP call into its own exception type rather than an
                        // OperationCanceledException — so key off the token, not the exception type. Unwind as a
                        // supersede (not a failure) so the newer turn composes the one consolidated reply and this turn
                        // does not log a spurious error or strand the conversation.
                        throw new OperationCanceledException("The SMS reply generation was superseded by a newer inbound message.", ex, generationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "AI Completion failed for Activity {ActivityId} using AI profile {ProfileId}.", activity.ItemId.SanitizeLogValue(), profile.ItemId.SanitizeLogValue());

                        break;
                    }

                    if (string.IsNullOrWhiteSpace(bestChoice))
                    {
                        _logger.LogWarning("AI Completion did not return any content for Activity {ActivityId} using AI profile {ProfileId}.", activity.ItemId.SanitizeLogValue(), profile.ItemId.SanitizeLogValue());

                        break;
                    }

                    // The model signals the end of the conversation with the hangup marker. Strip it so the customer never
                    // sees the control token, and remember that a conclusion was requested so the deferred analysis
                    // concludes the activity even if its own detection is unsure.
                    if (bestChoice.Contains(HangupMarker, StringComparison.Ordinal))
                    {
                        hangupRequested = true;
                        bestChoice = bestChoice.Replace(HangupMarker, string.Empty, StringComparison.Ordinal).Trim();
                    }

                    // handoffRequested was set from the transfer tool the model invoked during the completion above.
                    if (string.IsNullOrWhiteSpace(bestChoice))
                    {
                        // The model ended with only a marker and no words. Send the neutral bridge line for a handoff,
                        // or a neutral sign-off otherwise.
                        bestChoice = handoffRequested
                            ? S["Thanks! I'm connecting you with a specialist who will continue from here."].Value
                            : S["Thanks for your time. Goodbye."].Value;
                    }

                    // Humanized typing: a longer reply appears to take longer to type, so pause proportionally before it
                    // is sent (bounded so the turn stays well under the conversation lock lease).
                    var typingDelay = OmnichannelAutomationHelper.ResolveHumanizedTypingDelay(bestChoice.Length);

                    if (typingDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(typingDelay, generationToken);
                    }

                    // Final guard before sending: if any newer inbound message arrived while we settled, composed, or
                    // "typed", it has cancelled this turn. Do NOT send this now-stale reply — throw so the outer handler
                    // stops and the newer turn sends the one consolidated reply that accounts for the whole history.
                    generationToken.ThrowIfCancellationRequested();

                    bool sendSucceeded;

                    try
                    {
                        var result = await _smsService.SendAsync(new SmsMessage
                        {
                            To = activity.PreferredDestination,
                            From = endpoint.Value,
                            Body = bestChoice,
                        }, cancellationToken);

                        sendSucceeded = result.Succeeded;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send SMS message to {To} for Activity {ActivityId}.", _addressRedactor.Redact(activity.PreferredDestination), activity.ItemId.SanitizeLogValue());

                        break;
                    }

                    if (!sendSucceeded)
                    {
                        _logger.LogWarning("The SMS provider reported a failed send for Activity {ActivityId}; no reply was delivered this turn.", activity.ItemId.SanitizeLogValue());

                        break;
                    }

                    await _promptStore.CreateAsync(new AIChatSessionPrompt
                    {
                        ItemId = UniqueId.GenerateId(),
                        SessionId = chatSession.SessionId,
                        Role = ChatRole.Assistant,
                        Content = bestChoice,
                    }, cancellationToken);

                    chatSession.LastActivityUtc = _clock.UtcNow;
                    handledTurn = true;

                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Sent an automated SMS reply ({ReplyLength} chars); conversation {HangupState}.", bestChoice.Length, hangupRequested ? "flagged for conclusion" : "continuing");
                    }

                    // One consolidated reply per quiet period. We confirmed just above that no new customer message is
                    // waiting, so stop here; anything the customer sends from now on is handled as the next turn once the
                    // lock is released.
                    break;
                }

                // The bridge message has been sent; move the thread to a human and conclude the automated activity so
                // the AI never replies again (the terminal-status guard at the top of HandleAsync enforces that).
                if (handledTurn && handoffRequested)
                {
                    await PerformSmsHandoffAsync(activity, profile, chatSession.SessionId, flowSettings, endpoint.Value, handoffReason, smsHandoffService, cancellationToken);
                }

                if (handledTurn && !handoffRequested)
                {
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

                        // Serialize the conclusion's activity reads and writes against the next inbound turn on the
                        // SAME per-conversation lock the reply loop uses. The conclusion reads the activity, decides,
                        // then writes it; without the lock a later turn could interleave and both writes hit the same
                        // YesSql document version (the optimistic-concurrency conflict that previously rolled a turn
                        // back and produced a duplicate reply). Holding it across the AI call also means a new customer
                        // reply is not composed while we are still deciding whether the conversation has concluded.
                        var deferredLock = scope.ServiceProvider.GetRequiredService<ILocalLock>();

                        var (conclusionLocker, conclusionLocked) = await deferredLock.TryAcquireLockAsync(
                            $"SMS_CONVERSATION_{chatSession.SessionId}",
                            TimeSpan.FromSeconds(90),
                            TimeSpan.FromMinutes(2));

                        if (!conclusionLocked)
                        {
                            return;
                        }

                        await using var conclusionLockScope = conclusionLocker;

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

                        var client = await clientFactory.CreateChatClientAsync(deployment, builder => builder.UseDefaultResilience());

                        var contentManager = scope.ServiceProvider.GetRequiredService<IContentManager>();
                        var contentDefinitionManager = scope.ServiceProvider.GetRequiredService<IContentDefinitionManager>();

                        ContentItem subject = null;
                        ContentItem contact = null;

                        // The subject's TextField fields are the only structure the model may set; it is shown their keys
                        // ("Part.Field") and asked to return values, rather than authoring a content item (which produced
                        // shapes the field editors could not read and never persisted).
                        var subjectTextFields = activity.AllowAIToUpdateSubject && !string.IsNullOrWhiteSpace(activity.SubjectContentType)
                            ? GetSubjectTextFields(await contentDefinitionManager.GetTypeDefinitionAsync(activity.SubjectContentType))
                            : [];

                        var sessionPrompts = await deferredPromptStore.GetPromptsAsync(chatSession.SessionId);

                        var userPrompt = $"""

                        Current UTC time: {_clock.UtcNow:O}
                        Chat Summary: {JsonSerializer.Serialize(sessionPrompts)}
                        Subject Goal: {flowSettings.SubjectGoal}
                        List of Dispositions: {JsonSerializer.Serialize(dispositions.Select(x => new { Id = x.ItemId, x.Name, x.Description }))}

                        Decide whether the conversation has genuinely ended. Set Concluded to true ONLY when the exchange is clearly over: the agent has said goodbye or sent a closing message, or the customer has opted out, declined, or stopped engaging. Do NOT conclude while the agent is still asking a question or waiting for the customer to answer or confirm something (for example, right after the agent asked "is that correct?" the conversation is NOT concluded). When Concluded is true, select the single best DispositionId from the list above.
                        Always return Notes: a concise plain-text summary of the outcome to store on the account.
                        If, and only if, the customer clearly agreed to be contacted again at a specific time, set CallbackAtUtc to that moment as an absolute UTC timestamp (ISO 8601), resolving any relative time against the current UTC time above; otherwise leave CallbackAtUtc null.
                        If a follow-up is warranted, set NextActivityNotes to short preparation notes for whoever handles the next activity; otherwise leave it null.
                        {(subjectTextFields.Count > 0 ? "You are given a list of subject field keys. Return SubjectFields as a JSON object mapping the exact key shown to a short plain-text value, for any field the conversation clearly revealed; omit fields you did not learn and never invent keys." : "Do not return SubjectFields.")}
                        {(activity.AllowAIToUpdateContact ? "If, and only if, the customer clearly provided an email address for follow-up, set ContactEmail to that exact address (lowercased, no surrounding words); if it matches the current email on file or none was given, omit ContactEmail." : "Do not return ContactEmail.")}

                        """;

                        if (subjectTextFields.Count > 0)
                        {
                            subject ??= activity.Subject ?? await contentManager.NewAsync(activity.SubjectContentType);

                            userPrompt +=
                                $"""

                            Subject field keys: {JsonSerializer.Serialize(subjectTextFields.Select(f => $"{f.Part}.{f.Field}"))}
                            """;
                        }

                        if (activity.AllowAIToUpdateContact)
                        {
                            contact ??= await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

                            userPrompt +=
                                $"""

                            Current contact email: {GetContactEmail(contact) ?? "(none)"}
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

                            // Gated, field-aware subject write-back: set only known TextField fields, into their real Text
                            // structure, instead of merging a model-authored content item.
                            if (activity.AllowAIToUpdateSubject && subject is not null &&
                                ApplySubjectFields(subject, result.Result.SubjectFields, subjectTextFields))
                            {
                                omnichannelActivity ??= await store.FindByIdAsync(activity.ItemId);

                                omnichannelActivity.Subject = subject;

                                // Update the activity with the new subject since the converation may not be concluded.
                                await store.UpdateAsync(omnichannelActivity);
                            }

                            // Gated contact write-back: upsert only a captured email into the ContactMethods bag (mirroring
                            // how the importer builds it) instead of deep-merging a model-authored content item.
                            if (activity.AllowAIToUpdateContact && contact is not null &&
                                TryApplyContactEmail(contact, result.Result.ContactEmail))
                            {
                                await contentManager.UpdateAsync(contact);
                            }

                            if (result.Result.Concluded || hangupRequested)
                            {
                                if (flowSettings.RequireDisposition && string.IsNullOrEmpty(result.Result.DispositionId))
                                {
                                    _logger.LogWarning("The automated SMS conversation for Activity {ActivityId} reported concluded without a disposition, but its subject flow requires one. The activity is left open so the required-disposition policy is not bypassed; it will close through the existing no-response timeout or opt-out paths.", activity.ItemId.SanitizeLogValue());
                                }
                                else
                                {
                                    var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                                    var executor = scope.ServiceProvider.GetRequiredService<ISubjectActionExecutor>();

                                    omnichannelActivity ??= await store.FindByIdAsync(activity.ItemId);

                                    // Another writer (a concurrent turn or an earlier conclusion) may have already closed
                                    // this activity; don't re-complete or clobber it.
                                    if (omnichannelActivity is null ||
                                        omnichannelActivity.Status is ActivityStatus.Completed
                                            or ActivityStatus.Cancelled
                                            or ActivityStatus.Failed
                                            or ActivityStatus.Purged)
                                    {
                                        return;
                                    }

                                    omnichannelActivity.Status = ActivityStatus.Completed;

                                    if (_logger.IsEnabled(LogLevel.Information))
                                    {
                                        _logger.LogInformation("Concluding automated SMS conversation for Activity {ActivityId} with disposition {DispositionId}.", activity.ItemId.SanitizeLogValue(), (result.Result.DispositionId ?? "(none)").SanitizeLogValue());
                                    }

                                    omnichannelActivity.CompletedUtc = clock.UtcNow;

                                    omnichannelActivity.DispositionId = result.Result.DispositionId;

                                    omnichannelActivity.CompletedById = omnichannelActivity.AssignedToId;
                                    omnichannelActivity.CompletedByUsername = omnichannelActivity.AssignedToUsername;

                                    // Always notate the account on conclusion, mirroring the voice channel: the AI summary
                                    // becomes the activity notes, falling back to a default line when the model returned none.
                                    omnichannelActivity.Notes = string.IsNullOrWhiteSpace(result.Result.Notes)
                                        ? "Automated AI SMS conversation completed."
                                        : result.Result.Notes.Trim();

                                    await store.UpdateAsync(omnichannelActivity);

                                    subject ??= activity.Subject ?? await contentManager.NewAsync(activity.SubjectContentType);
                                    contact ??= await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

                                    var dispositionObj = dispositions.FirstOrDefault(d => d.ItemId == result.Result.DispositionId);

                                    // Carry the AI-detected callback time and follow-up notes into the disposition's
                                    // follow-up actions (TryAgain / NewActivity). The executor keys the follow-up activity's
                                    // schedule date and instructions by subject-action id, so map both onto every follow-up
                                    // action of the selected disposition. When the AI returned neither, the executor keeps
                                    // its own defaults (the action's default schedule window and fallback instructions).
                                    Dictionary<string, DateTime?> actionScheduleDates = null;
                                    Dictionary<string, string> actionPreparationNotes = null;

                                    if (dispositionObj is not null &&
                                        (result.Result.CallbackAtUtc.HasValue || !string.IsNullOrWhiteSpace(result.Result.NextActivityNotes)))
                                    {
                                        var followUpActionIds = allActions
                                            .Where(a => string.Equals(a.SubjectContentType, activity.SubjectContentType, StringComparison.OrdinalIgnoreCase)
                                                     && string.Equals(a.DispositionId, dispositionObj.ItemId, StringComparison.OrdinalIgnoreCase)
                                                     && (string.Equals(a.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase)
                                                      || string.Equals(a.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase)))
                                            .Select(a => a.ItemId)
                                            .ToList();

                                        if (followUpActionIds.Count > 0)
                                        {
                                            if (result.Result.CallbackAtUtc.HasValue)
                                            {
                                                var callbackUtc = DateTime.SpecifyKind(result.Result.CallbackAtUtc.Value, DateTimeKind.Utc);
                                                actionScheduleDates = followUpActionIds.ToDictionary(id => id, _ => (DateTime?)callbackUtc);
                                            }

                                            if (!string.IsNullOrWhiteSpace(result.Result.NextActivityNotes))
                                            {
                                                var prep = result.Result.NextActivityNotes.Trim();
                                                actionPreparationNotes = followUpActionIds.ToDictionary(id => id, _ => prep);
                                            }
                                        }
                                    }

                                    await executor.ExecuteAsync(new SubjectActionExecutionContext
                                    {
                                        Activity = omnichannelActivity,
                                        Contact = contact,
                                        Subject = subject,
                                        Disposition = dispositionObj,
                                        ActionScheduleDates = actionScheduleDates,
                                        ActionPreparationNotes = actionPreparationNotes,
                                    });
                                }
                            }
                        }
                    });

                    await _session.SaveAsync(chatSession, cancellationToken: cancellationToken);
                }
            }

            await _session.SaveAsync(chatSession, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (generationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // A newer inbound message for this same conversation superseded this turn: it cancelled this generation
            // and will compose the single consolidated reply against the fuller transcript. Stop quietly.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("SMS reply generation for session {SessionId} was superseded by a newer inbound message.", chatSession.SessionId.SanitizeLogValue());
            }
        }
    }

    // Hands the automated SMS conversation off to a live agent: it hydrates the human thread with the transcript
    // (via the resolved SMS handoff service) and, on success, concludes the automated activity as handed-off so the
    // AI never replies again. On failure it leaves the activity awaiting the customer so the conversation is not
    // stranded after the bridge message was already sent.
    private async Task PerformSmsHandoffAsync(
        OmnichannelActivity activity,
        AIProfile profile,
        string sessionId,
        SubjectFlowSettings flowSettings,
        string serviceAddress,
        string reason,
        IOmnichannelHandoffService handoffService,
        CancellationToken cancellationToken)
    {
        var conversation = (await _promptStore.GetPromptsAsync(sessionId))
            .Where(prompt => !prompt.IsGeneratedPrompt)
            .ToList();

        var prompts = conversation
            .Select(prompt => new OmnichannelHandoffMessage
            {
                IsInbound = prompt.Role == ChatRole.User,
                Content = prompt.Content,
                CreatedUtc = prompt.CreatedUtc,
            })
            .ToList();

        // Warm context for the agent taking over: a short AI-written summary of what happened. Best-effort — a
        // summary failure must never block the handoff.
        var summary = await GenerateHandoffSummaryAsync(profile, conversation, activity, cancellationToken);

        OmnichannelHandoffResult result;

        try
        {
            result = await handoffService.RequestHandoffAsync(new OmnichannelHandoffRequest
            {
                Activity = activity,
                TargetQueueId = flowSettings.HandoffQueueId,
                Reason = string.IsNullOrWhiteSpace(reason)
                    ? "The automated assistant escalated the conversation to a live agent."
                    : reason,
                Summary = summary,
                ServiceAddress = serviceAddress,
                ContactAddress = activity.PreferredDestination,
                Transcript = prompts,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The SMS handoff for Activity {ActivityId} threw; leaving the conversation open.", activity.ItemId.SanitizeLogValue());
            result = OmnichannelHandoffResult.Failure(ex.Message);
        }

        if (!result.Succeeded)
        {
            // The bridge message was already sent, but there is no human thread to continue in. Keep the activity
            // awaiting the customer so the automated agent can still respond rather than going silent.
            _logger.LogWarning("The SMS handoff for Activity {ActivityId} did not complete: {Reason}. The conversation stays with the automated agent.", activity.ItemId.SanitizeLogValue(), result.Message);

            activity.Status = ActivityStatus.AwaitingCustomerAnswer;

            if (OmnichannelAutomationHelper.HasNoResponseTimeout(flowSettings))
            {
                activity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(flowSettings, _clock.UtcNow);
            }

            await _omnichannelActivityStore.UpdateAsync(activity, cancellationToken);

            return;
        }

        // Conclude the automated activity as handed off. This is a distinct terminal path from a natural conclusion:
        // no disposition executor runs, and the terminal reason lets reporting separate escalations from bot-contained
        // conversations.
        activity.Status = ActivityStatus.Completed;
        activity.CompletedUtc = _clock.UtcNow;
        activity.TerminalReasonCode = OmnichannelConstants.TerminalReasons.HandedOffToAgent;
        activity.AiEscalated = true;
        activity.CompletedById = activity.AssignedToId;
        activity.CompletedByUsername = activity.AssignedToUsername;

        if (string.IsNullOrWhiteSpace(activity.Notes))
        {
            activity.Notes = "The automated SMS conversation was handed off to a live agent.";
        }

        await _omnichannelActivityStore.UpdateAsync(activity, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Concluded automated SMS Activity {ActivityId} as handed off to a live agent (conversation {ConversationId}).", activity.ItemId.SanitizeLogValue(), (result.ConversationId ?? "(none)").SanitizeLogValue());
        }
    }

    // Produces a short, plain-text summary of the conversation for the agent taking over. Best-effort: any failure
    // returns null so the handoff proceeds without a summary.
    private async Task<string> GenerateHandoffSummaryAsync(
        AIProfile profile,
        List<AIChatSessionPrompt> conversation,
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        const string summaryPrompt =
            "You are handing this SMS conversation to a human agent. In 2-3 short sentences of plain text (no " +
            "preamble, labels, or quotes), summarize for the agent what the customer wants, the key facts they " +
            "shared, and why they are being transferred.";

        try
        {
            var context = await _completionContextBuilder.BuildAsync(profile, builder =>
            {
                builder.SystemMessage = summaryPrompt;
                builder.DisableTools = true;
            }, cancellationToken);

            var deployment = await _deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentPurpose.Chat,
                deploymentName: context.ChatDeploymentName,
                cancellationToken: cancellationToken);

            if (deployment is null)
            {
                return null;
            }

            var client = await _aiClientFactory.CreateChatClientAsync(deployment, builder => builder.UseDefaultResilience());

            var messages = new List<ChatMessage> { new(ChatRole.System, summaryPrompt) };
            messages.AddRange(conversation.Select(prompt => new ChatMessage(prompt.Role, prompt.Content)));

            var response = await client.GetResponseAsync(messages, cancellationToken: cancellationToken);

            return response?.Text?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate an SMS handoff summary for Activity {ActivityId}; continuing without one.", activity.ItemId.SanitizeLogValue());

            return null;
        }
    }

    // Attaches the transfer-to-agent tool to this single completion. The automated conversation calls the completion
    // service directly rather than through the tool orchestrator, so the scoped-tool key the function-invocation
    // service handler reads is otherwise never populated here and no tools reach the model at all. We register the
    // transfer tool as a scoped system-tool entry for this turn only (the context is built per turn and never
    // persisted), so the model can actually invoke it. Enabling it through the profile's tool-name list does not
    // work: the profile tool provider reads the names snapshotted when the context was built and, either way, skips
    // system tools — which the transfer tool is.
    private static void AttachTransferToAgentTool(AICompletionContext context)
    {
        var entry = new ToolRegistryEntry
        {
            Id = OmnichannelHandoffHelper.TransferToAgentToolName,
            Name = OmnichannelHandoffHelper.TransferToAgentToolName,
            Description = "Transfers the current conversation to a live human agent.",
            Source = ToolRegistryEntrySource.System,
            CreateAsync = serviceProvider => ValueTask.FromResult(
                serviceProvider.GetKeyedService<AITool>(OmnichannelHandoffHelper.TransferToAgentToolName)),
        };

        context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] =
            new List<ToolRegistryEntry> { entry };
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

    private async Task ApplySmsOptOutAsync(
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        if (contact is null)
        {
            _logger.LogWarning("Unable to update Do not SMS for Activity {ActivityId} because contact {ContactContentItemId} was not found.", activity.ItemId.SanitizeLogValue(), activity.ContactContentItemId.SanitizeLogValue());
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

    // Field-aware subject/contact write-back. This mirrors the voice channel's approach (in
    // TelnyxAiVoiceConversationHandler): write only known TextField fields into their real Text structure, and upsert
    // a captured email into the ContactMethods bag, instead of merging a model-authored content item (which the
    // editors could not read and never persisted). Kept private here to avoid coupling the SMS module to Telnyx;
    // consolidating both into a shared Omnichannel helper is a worthwhile future cleanup.

    // Registers this handler as the single active AI generation for a conversation, cancelling any generation still in
    // flight for the same conversation — the customer's newer inbound message makes the older reply stale. The returned
    // token is cancelled if a still-newer message arrives, so the whole turn unwinds and only the newest turn sends.
    // A single node owns each conversation's inbound processing, so an in-memory registry keyed by session is enough.
    // Whether a reply is being composed right now for this conversation on this node. The owed-reply recovery task
    // uses this to skip conversations a live inbound is already handling, so it only re-drives ones whose generation
    // was genuinely lost (for example after a restart, when this in-memory registry starts empty).
    internal static bool IsGenerating(string sessionId)
        => !string.IsNullOrEmpty(sessionId) && _activeGenerations.ContainsKey(sessionId);

    private static GenerationRegistration RegisterGeneration(string sessionId, CancellationToken hostToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(hostToken);

        _activeGenerations.AddOrUpdate(
            sessionId,
            cts,
            (_, existing) =>
            {
                // A reply is already being composed for this conversation. Cancel it — the newer inbound message makes
                // it stale — and take its place. The superseded turn disposes its own source in its finally.
                existing.Cancel();

                return cts;
            });

        return new GenerationRegistration(sessionId, cts);
    }

    // Scopes an active-generation registration: exposes the cancellation token and, on dispose, deregisters this turn
    // (only if it is still the active one — a newer turn may have replaced it) and disposes the linked source.
    private sealed class GenerationRegistration : IDisposable
    {
        private readonly string _sessionId;
        private readonly CancellationTokenSource _cts;

        public GenerationRegistration(string sessionId, CancellationTokenSource cts)
        {
            _sessionId = sessionId;
            _cts = cts;
        }

        public CancellationToken Token => _cts.Token;

        public void Dispose()
        {
            // Only remove ourselves if we are still the registered generation; a newer turn may already own the slot.
            _activeGenerations.TryRemove(new KeyValuePair<string, CancellationTokenSource>(_sessionId, _cts));
            _cts.Dispose();
        }
    }

    // Returns the customer (user) messages that trail the transcript after the last assistant reply — i.e. the
    // messages the automated agent has not answered yet. An empty result means the last thing said was the agent's
    // own reply, so nothing is owed and the conversation should not send again on its own. The initial outbound is
    // stored as an assistant message, so this is well-defined from the very first turn.
    private async Task<bool> ShouldRespondAsync(
        AIProfile profile,
        List<ChatMessage> conversation,
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = await _completionContextBuilder.BuildAsync(profile, builder =>
            {
                builder.SystemMessage = ShouldRespondPrompt;
                builder.DisableTools = true;
            }, cancellationToken);

            var deployment = await _deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentPurpose.Chat,
                deploymentName: context.ChatDeploymentName,
                cancellationToken: cancellationToken);

            if (deployment is null)
            {
                return true;
            }

            var client = await _aiClientFactory.CreateChatClientAsync(deployment, builder => builder.UseDefaultResilience());

            var messages = new List<ChatMessage>
            {
                new (ChatRole.System, ShouldRespondPrompt),
            };

            messages.AddRange(conversation);

            var decision = await client.GetResponseAsync<ShouldRespondResult>(messages, _jsonSerializerOptions.SerializerOptions, cancellationToken: cancellationToken);

            if (decision.Result is { ShouldRespond: false })
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Skipping an SMS reply for Activity {ActivityId} because a reply was judged unwarranted. Reason: {Reason}", activity.ItemId.SanitizeLogValue(), decision.Result.Reason);
                }

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Fail open: if the decision cannot be made, reply as normal rather than going silent on the customer.
            _logger.LogWarning(ex, "The should-respond evaluation failed for Activity {ActivityId}; replying by default.", activity.ItemId.SanitizeLogValue());

            return true;
        }
    }

    private static List<AIChatSessionPrompt> GetTrailingUserMessages(List<AIChatSessionPrompt> conversation)
    {
        var pending = new List<AIChatSessionPrompt>();

        if (conversation is null)
        {
            return pending;
        }

        for (var i = conversation.Count - 1; i >= 0; i--)
        {
            var prompt = conversation[i];

            if (prompt.Role == ChatRole.Assistant)
            {
                break;
            }

            if (prompt.Role == ChatRole.User)
            {
                pending.Add(prompt);
            }
        }

        pending.Reverse();

        return pending;
    }

    private static string GetContactEmail(ContentItem contact)
    {
        if (contact is null ||
            !contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bag) || bag.ContentItems is null)
        {
            return null;
        }

        foreach (var method in bag.ContentItems)
        {
            if (string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal) &&
                method.TryGet<EmailInfoPart>(out var emailPart) &&
                !string.IsNullOrWhiteSpace(emailPart.Email?.Text))
            {
                return emailPart.Email.Text.Trim();
            }
        }

        return null;
    }

    private static bool TryApplyContactEmail(ContentItem contact, string email)
    {
        if (contact is null || string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        email = email.Trim();

        // A conservative sanity check so a mis-parsed phrase is never written as an email.
        if (email.Length < 5 || !email.Contains('@', StringComparison.Ordinal) || email.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        // Nothing to do when the same address is already on file.
        if (string.Equals(GetContactEmail(contact), email, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var bag = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        bag.ContentItems ??= [];
        bag.ContentItems.RemoveAll(method => string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal));

        var emailItem = new ContentItem
        {
            ContentType = OmnichannelConstants.ContentTypes.EmailAddress,
            DisplayText = email,
        };

        emailItem.Alter<EmailInfoPart>(part => part.Email = new TextField { Text = email });
        bag.ContentItems.Add(emailItem);
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return true;
    }

    private static List<(string Part, string Field)> GetSubjectTextFields(ContentTypeDefinition typeDefinition)
    {
        var fields = new List<(string, string)>();

        if (typeDefinition is null)
        {
            return fields;
        }

        foreach (var part in typeDefinition.Parts)
        {
            foreach (var field in part.PartDefinition.Fields)
            {
                if (string.Equals(field.FieldDefinition?.Name, nameof(TextField), StringComparison.Ordinal))
                {
                    fields.Add((part.Name, field.Name));
                }
            }
        }

        return fields;
    }

    private static bool ApplySubjectFields(ContentItem subject, Dictionary<string, string> values, List<(string Part, string Field)> fields)
    {
        if (subject is null || values is null || values.Count == 0 || fields.Count == 0)
        {
            return false;
        }

        var changed = false;

        // ContentItem.Content is a dynamic JsonDynamicObject; cast to the underlying JsonObject so type checks and
        // writes operate on the real node (accessing through the dynamic hands back a wrapper that is never a
        // JsonObject, which made each field clobber the previous one).
        var content = (JsonObject)subject.Content;

        foreach (var (part, field) in fields)
        {
            if (!(values.TryGetValue($"{part}.{field}", out var value) || values.TryGetValue(field, out value)) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (content[part] is not JsonObject partObject)
            {
                partObject = new JsonObject();
                content[part] = partObject;
            }

            partObject[field] = new JsonObject { ["Text"] = value.Trim() };
            changed = true;
        }

        return changed;
    }

    private sealed class ShouldRespondResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the agent should send a reply to the customer's latest message now.
        /// </summary>
        public bool ShouldRespond { get; set; }

        /// <summary>
        /// Gets or sets a short reason for the decision.
        /// </summary>
        public string Reason { get; set; }
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
        /// Gets or sets a concise summary of the conversation outcome, stored as the notes on the completed
        /// activity so a dispositioned conversation is always notated.
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// Gets or sets the absolute UTC date and time the customer clearly agreed to be contacted again, when
        /// one was established; otherwise null. It schedules the follow-up activity the disposition creates.
        /// </summary>
        public DateTime? CallbackAtUtc { get; set; }

        /// <summary>
        /// Gets or sets short preparation notes for the follow-up activity the disposition creates, when a
        /// follow-up is warranted; otherwise null.
        /// </summary>
        public string NextActivityNotes { get; set; }

        /// <summary>
        /// Gets or sets the subject field values the AI captured, keyed by the "Part.Field" path shown to it, when
        /// allowed. Null otherwise.
        /// </summary>
        public Dictionary<string, string> SubjectFields { get; set; }

        /// <summary>
        /// Gets or sets the email address the customer provided for follow-up, when allowed. Null otherwise.
        /// </summary>
        public string ContactEmail { get; set; }
    }
}
