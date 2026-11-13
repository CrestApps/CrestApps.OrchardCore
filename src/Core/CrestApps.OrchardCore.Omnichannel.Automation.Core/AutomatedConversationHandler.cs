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
using CrestApps.Core.Support;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Json;
using OrchardCore.Locking;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Answers a customer's message in an automated (AI) conversation, on any channel with an
/// <see cref="IAutomatedMessagingChannel"/>: it records the message, decides whether a reply is warranted, composes one
/// reply for everything the customer sent since the last, sends it through the channel, hands the customer to a person
/// when the AI asks to, and judges afterwards whether the conversation has concluded and with which disposition.
/// </summary>
/// <remarks>
/// One reply per conversation at a time: a newer message cancels the reply still being composed for an older one, and
/// a per-conversation lock serializes the sends, so a burst of messages gets one consolidated answer and a redelivered
/// message is never answered twice.
/// </remarks>
public sealed partial class AutomatedConversationHandler : IOmnichannelEventHandler
{
    // The shared AI profile prompt appends this control marker when it decides the conversation is over (the voice
    // channel uses it to hang up). It is stripped from the message the customer receives, and it is a reliable signal to
    // conclude the activity.
    private const string HangupMarker = "[[HANGUP]]";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly IEnumerable<IAutomatedMessagingChannel> _channels;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAICompletionService _completionService;
    private readonly IOmnichannelHandoffTurn _handoffTurn;
    private readonly IAutomatedConversationGate _conversationGate;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionContextBuilder _completionContextBuilder;
    private readonly IAIProfileManager _profileManager;
    private readonly ITemplateService _templateService;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IContentManager _contentManager;
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IEnumerable<IOmnichannelHandoffService> _handoffServices;
    private readonly IEnumerable<IMessagingAIConversationStarter> _conversationStarters;
    private readonly ILocalLock _localLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly DocumentJsonSerializerOptions _jsonSerializerOptions;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomatedConversationHandler"/> class.
    /// </summary>
    /// <param name="channels">The channels automated conversations run on.</param>
    /// <param name="channelResolver">The resolver of the messaging channels replies are sent through.</param>
    /// <param name="chatSessionManager">The AI chat session manager.</param>
    /// <param name="promptStore">The AI chat transcript store.</param>
    /// <param name="completionService">The AI completion service.</param>
    /// <param name="handoffTurn">The scoped record of a handoff the model asked for during a completion.</param>
    /// <param name="conversationGate">The registry of the reply being composed for each conversation.</param>
    /// <param name="aiClientFactory">The AI client factory.</param>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    /// <param name="completionContextBuilder">The AI completion context builder.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="templateService">The prompt template service.</param>
    /// <param name="endpointManager">The manager of the business's addresses.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="activityStore">The activity store.</param>
    /// <param name="handoffServices">The services a conversation is handed to a person through.</param>
    /// <param name="conversationStarters">The starters that open an AI conversation for a message to an AI-routed address.</param>
    /// <param name="localLock">The in-process lock that serializes one conversation's replies.</param>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="jsonSerializerOptions">The document serializer options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AutomatedConversationHandler(
        IEnumerable<IAutomatedMessagingChannel> channels,
        IMessagingChannelResolver channelResolver,
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAICompletionService completionService,
        IOmnichannelHandoffTurn handoffTurn,
        IAutomatedConversationGate conversationGate,
        IAIClientFactory aiClientFactory,
        IAIDeploymentManager deploymentManager,
        IAICompletionContextBuilder completionContextBuilder,
        IAIProfileManager profileManager,
        ITemplateService templateService,
        IOmnichannelChannelEndpointManager endpointManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IContentManager contentManager,
        IOmnichannelActivityStore activityStore,
        IEnumerable<IOmnichannelHandoffService> handoffServices,
        IEnumerable<IMessagingAIConversationStarter> conversationStarters,
        ILocalLock localLock,
        ISession session,
        IClock clock,
        IOptions<DocumentJsonSerializerOptions> jsonSerializerOptions,
        ILogger<AutomatedConversationHandler> logger,
        IStringLocalizer<AutomatedConversationHandler> stringLocalizer)
    {
        _channels = channels;
        _channelResolver = channelResolver;
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _completionService = completionService;
        _handoffTurn = handoffTurn;
        _conversationGate = conversationGate;
        _aiClientFactory = aiClientFactory;
        _deploymentManager = deploymentManager;
        _completionContextBuilder = completionContextBuilder;
        _profileManager = profileManager;
        _templateService = templateService;
        _endpointManager = endpointManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _contentManager = contentManager;
        _activityStore = activityStore;
        _handoffServices = handoffServices;
        _conversationStarters = conversationStarters;
        _localLock = localLock;
        _session = session;
        _clock = clock;
        _jsonSerializerOptions = jsonSerializerOptions.Value;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the key of the lock that serializes one automated conversation's replies, conclusion and follow-ups.
    /// </summary>
    /// <param name="sessionId">The conversation's AI chat session.</param>
    /// <returns>The lock key.</returns>
    public static string GetLockKey(string sessionId)
        => $"AUTOMATED_CONVERSATION_{sessionId}";

    /// <inheritdoc/>
    public async Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
    {
        var message = omnichannelEvent?.Message;

        if (message is null || !message.IsInbound)
        {
            return;
        }

        var automatedChannel = _channels.FirstOrDefault(candidate =>
            string.Equals(candidate.ReceivedEventType, omnichannelEvent.EventType, StringComparison.Ordinal) &&
            string.Equals(candidate.Channel, message.Channel, StringComparison.OrdinalIgnoreCase));

        var messagingChannel = automatedChannel is null ? null : _channelResolver.Get(automatedChannel.Channel);

        if (automatedChannel is null || messagingChannel is null)
        {
            return;
        }

        var serviceAddress = automatedChannel.NormalizeAddress(message.ServiceAddress);
        var customerAddress = automatedChannel.NormalizeAddress(message.CustomerAddress);
        var endpoint = string.IsNullOrEmpty(serviceAddress)
            ? null
            : await _endpointManager.GetByServiceAddressAsync(automatedChannel.Channel, serviceAddress, cancellationToken);

        if (endpoint is null)
        {
            _logger.LogWarning("No address matches an inbound {Channel} message, so the automated agent leaves it to the other handlers.", automatedChannel.Channel);

            return;
        }

        // A machine wrote it (an out-of-office, a bounce). Answering would let two automated systems talk forever, and
        // keeping it in the transcript would leave a reply owed that the recovery task would later send.
        if (automatedChannel.IsAutomaticMessage(message))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Ignoring an automatic {Channel} message for the automated agent.", automatedChannel.Channel);
            }

            return;
        }

        var activity = await _activityStore.GetAsync(automatedChannel.Channel, endpoint.GetKnownIds(), customerAddress, ActivityInteractionType.Automated, cancellationToken);

        // An address whose entry point routes to an AI agent has the AI take the customer's first message. The starter is
        // shared with the messaging workspace, so whichever handles the message first starts the conversation, and it is
        // read back here as the starter committed it.
        if ((activity is null || activity.Status.IsTerminal()) && await StartConversationAsync(message, endpoint, cancellationToken))
        {
            activity = await _activityStore.GetAsync(automatedChannel.Channel, endpoint.GetKnownIds(), customerAddress, ActivityInteractionType.Automated, cancellationToken);
        }

        // No automated conversation for the customer is the ordinary case for a person-to-person thread: the
        // workspace's inbound pipeline, another handler of the same event, records and routes it.
        if (activity is null)
        {
            return;
        }

        // A concluded, cancelled, failed or purged conversation has ended. A later "thanks" is history, not a reason to
        // start answering again.
        if (activity.Status is ActivityStatus.Completed or ActivityStatus.Cancelled or ActivityStatus.Failed or ActivityStatus.Purged)
        {
            return;
        }

        var flowSettings = await FindFlowSettingsAsync(activity.SubjectContentType, cancellationToken);

        if (automatedChannel.IsOptOutRequest(message, flowSettings))
        {
            await ApplyOptOutAsync(automatedChannel, activity, cancellationToken);

            return;
        }

        if (flowSettings is null)
        {
            if (!string.IsNullOrWhiteSpace(activity.SubjectContentType))
            {
                _logger.LogWarning("The subject flow settings for subject '{SubjectContentType}' of Activity {ActivityId} were not found, so the {Channel} message is not answered.", activity.SubjectContentType.SanitizeLogValue(), activity.ItemId.SanitizeLogValue(), automatedChannel.Channel);

                return;
            }

            // An entry point's AI agent answering an address with no inbound subject converses on its profile alone,
            // with no dispositions to choose from and no handoff.
            flowSettings = new SubjectFlowSettings();
        }

        var (profile, chatSession) = await LoadConversationAsync(activity, flowSettings, cancellationToken);

        if (profile is null || chatSession is null)
        {
            return;
        }

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ActivityId"] = activity.ItemId,
            ["SessionId"] = chatSession.SessionId,
            ["Channel"] = automatedChannel.Channel,
        });

        await RecordCustomerMessageAsync(chatSession, message, cancellationToken);

        var turn = new AutomatedConversationTurn
        {
            Channel = automatedChannel,
            MessagingChannel = messagingChannel,
            Activity = activity,
            Endpoint = endpoint,
            FlowSettings = flowSettings,
            Profile = profile,
            ChatSession = chatSession,
            ReplyTo = await ResolveReplyToAsync(automatedChannel, message, endpoint, customerAddress, cancellationToken),
        };

        await RunTurnAsync(turn, cancellationToken);
    }

    // The profile and transcript of the conversation. The session's own profile wins, since it is the one the
    // conversation began with.
    private async Task<(AIProfile Profile, AIChatSession Session)> LoadConversationAsync(OmnichannelActivity activity, SubjectFlowSettings flowSettings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            _logger.LogWarning("Activity {ActivityId} has no AI session, so the automated agent cannot answer it.", activity.ItemId.SanitizeLogValue());

            return (null, null);
        }

        var chatSession = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (chatSession is null)
        {
            _logger.LogWarning("The AI session of Activity {ActivityId} was not found, so the automated agent cannot answer it.", activity.ItemId.SanitizeLogValue());

            return (null, null);
        }

        var profileId = !string.IsNullOrWhiteSpace(chatSession.ProfileId)
            ? chatSession.ProfileId
            : string.IsNullOrWhiteSpace(activity.AIProfileId) ? flowSettings.ProfileId : activity.AIProfileId;

        var profile = string.IsNullOrWhiteSpace(profileId) ? null : await _profileManager.FindByIdAsync(profileId, cancellationToken);

        if (profile is null || profile.Type != AIProfileType.Chat)
        {
            _logger.LogWarning("The AI profile '{ProfileId}' of Activity {ActivityId} was not found or is not a chat profile.", (profileId ?? "(none)").SanitizeLogValue(), activity.ItemId.SanitizeLogValue());

            return (null, null);
        }

        return (profile, chatSession);
    }

    // Stores the customer's message unless it is already the trailing turn, which makes the handler idempotent: a
    // redelivery, or the owed-reply recovery re-driving a reply lost to a restart, must not add the same turn twice.
    private async Task RecordCustomerMessageAsync(AIChatSession chatSession, OmnichannelMessage message, CancellationToken cancellationToken)
    {
        var existing = (await _promptStore.GetPromptsAsync(chatSession.SessionId))
            .Where(prompt => !prompt.IsGeneratedPrompt)
            .ToList();

        var last = existing.LastOrDefault();

        var alreadyStored = last is not null &&
            last.Role == ChatRole.User &&
            string.Equals(last.Content?.Trim(), message.Content?.Trim(), StringComparison.Ordinal);

        if (!alreadyStored)
        {
            await _promptStore.CreateAsync(AutomatedConversationTranscript.CreateCustomerPrompt(chatSession.SessionId, message, _clock.UtcNow), cancellationToken);
        }
    }

    private async Task RunTurnAsync(AutomatedConversationTurn turn, CancellationToken cancellationToken)
    {
        var activity = turn.Activity;
        var chatSession = turn.ChatSession;

        // One reply per conversation at a time. This turn becomes the active generation, cancelling a reply still being
        // composed for an older message, which the newer message makes stale.
        using var generation = _conversationGate.Begin(chatSession.SessionId, cancellationToken);
        var generationToken = generation.Token;

        var handoffService = _handoffServices.FirstOrDefault(service => service.CanHandle(turn.Channel.Channel));
        var handoffAvailable = handoffService is not null && OmnichannelHandoffHelper.IsHandoffEnabled(turn.FlowSettings);

        try
        {
            var (locker, locked) = await _localLock.TryAcquireLockAsync(GetLockKey(chatSession.SessionId), _lockTimeout, _lockExpiration);

            if (!locked)
            {
                _logger.LogWarning("Timed out waiting for the conversation lock of Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());

                return;
            }

            await using (locker)
            {
                var outcome = await ReplyAsync(turn, handoffAvailable, generationToken, cancellationToken);

                if (outcome.Handled && outcome.HandoffRequested)
                {
                    await PerformHandoffAsync(turn, outcome.HandoffReason, handoffService, cancellationToken);
                }
                else if (outcome.Handled)
                {
                    activity.Status = ActivityStatus.AwaitingCustomerAnswer;

                    if (OmnichannelAutomationHelper.HasNoResponseTimeout(turn.FlowSettings))
                    {
                        activity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(turn.FlowSettings, _clock.UtcNow);
                    }

                    await _activityStore.UpdateAsync(activity, cancellationToken);

                    ScheduleConclusion(turn, outcome.HangupRequested);
                }
            }

            await _session.SaveAsync(chatSession, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (generationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // A newer message superseded this turn; it composes the one consolidated reply. Stop quietly.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("The automated reply for session {SessionId} was superseded by a newer message.", chatSession.SessionId.SanitizeLogValue());
            }
        }
    }

    private async Task<AutomatedTurnOutcome> ReplyAsync(AutomatedConversationTurn turn, bool handoffAvailable, CancellationToken generationToken, CancellationToken cancellationToken)
    {
        var outcome = new AutomatedTurnOutcome();
        var activity = turn.Activity;
        var chatSession = turn.ChatSession;

        // The reply delay chosen when the activities were loaded, else the subject flow's own for the channel. It is the
        // floor of the reading pause below.
        var configuredDelay = OmnichannelAutomationHelper.ResolveResponseDelay(activity.ResponseDelayMode, activity.ResponseDelaySeconds, activity.ResponseDelayJitterSeconds)
            ?? turn.Channel.GetDefaultResponseDelay(turn.FlowSettings);

        // Only the customer messages since the last reply are owed an answer. When an earlier pass already answered them,
        // nothing is pending and nothing is sent, which is what keeps concurrent handlers from replying twice.
        var pending = AutomatedConversationTranscript.GetTrailingUserMessages(await GetTranscriptAsync(chatSession));

        if (pending.Count == 0)
        {
            return outcome;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Composing an automated {Channel} reply; {PendingCount} customer message(s) owed a response.", turn.Channel.Channel, pending.Count);
        }

        var readingDelay = turn.Channel.GetReadingDelay(configuredDelay, pending.Sum(prompt => prompt.Content?.Length ?? 0));

        if (readingDelay > TimeSpan.Zero)
        {
            await Task.Delay(readingDelay, generationToken);
        }

        // Re-read after the pause, so messages that arrived meanwhile are answered by this one reply.
        var transcript = (await GetTranscriptAsync(chatSession))
            .Select(prompt => new ChatMessage(prompt.Role, prompt.Content))
            .ToList();

        if (!await ShouldRespondAsync(turn, transcript, generationToken))
        {
            // The agent chose not to reply. The turn is still handled, so the activity advances and the conclusion check
            // runs, which closes a conversation that has naturally ended.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("The automated agent chose not to reply to the current {Channel} turn.", turn.Channel.Channel);
            }

            outcome.Handled = true;

            return outcome;
        }

        var reply = await ComposeAsync(turn, transcript, handoffAvailable, outcome, generationToken, cancellationToken);

        if (string.IsNullOrWhiteSpace(reply))
        {
            return outcome;
        }

        var typingDelay = turn.Channel.GetTypingDelay(reply.Length);

        if (typingDelay > TimeSpan.Zero)
        {
            await Task.Delay(typingDelay, generationToken);
        }

        // A newer message that arrived while composing cancelled this turn; the stale reply is not sent.
        generationToken.ThrowIfCancellationRequested();

        MessageDispatchResultView result;

        try
        {
            var dispatch = await turn.MessagingChannel.SendAsync(new MessagingOutboundMessage
            {
                ServiceAddress = turn.Endpoint.Value,
                ContactAddress = activity.PreferredDestination,
                ReplyTo = turn.ReplyTo,
                Purpose = MessagingOutboundPurpose.Automation,
                Body = reply,
            }, cancellationToken);

            result = new MessageDispatchResultView(dispatch.Succeeded, dispatch.GetErrorText());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Sending the automated {Channel} reply for Activity {ActivityId} failed.", turn.Channel.Channel, activity.ItemId.SanitizeLogValue());

            return outcome;
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("The {Channel} provider refused the automated reply for Activity {ActivityId}: {Error}", turn.Channel.Channel, activity.ItemId.SanitizeLogValue(), result.Error.SanitizeLogValue());

            return outcome;
        }

        await _promptStore.CreateAsync(new AIChatSessionPrompt
        {
            ItemId = UniqueId.GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.Assistant,
            Content = reply,

            // The reply's time orders it after the customer's message; without it the owed-reply scan and the transcript
            // order break.
            CreatedUtc = _clock.UtcNow,
        }, cancellationToken);

        // Committed while the lock is still held, so the next turn, which takes the lock next, sees this reply and does
        // not answer the same message again.
        await _session.SaveChangesAsync(cancellationToken);

        chatSession.LastActivityUtc = _clock.UtcNow;
        outcome.Handled = true;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Sent an automated {Channel} reply ({ReplyLength} chars); conversation {HangupState}.", turn.Channel.Channel, reply.Length, outcome.HangupRequested ? "flagged for conclusion" : "continuing");
        }

        return outcome;
    }

    private async Task<string> ComposeAsync(
        AutomatedConversationTurn turn,
        List<ChatMessage> transcript,
        bool handoffAvailable,
        AutomatedTurnOutcome outcome,
        CancellationToken generationToken,
        CancellationToken cancellationToken)
    {
        string reply;

        try
        {
            var context = await _completionContextBuilder.BuildAsync(turn.Profile, cancellationToken: generationToken);
            context.AdditionalProperties["Session"] = turn.ChatSession;

            // The subject the customer wrote under says what the conversation is about, on a channel that has one.
            var subject = turn.ReplyTo?.GetSubject();

            if (!string.IsNullOrWhiteSpace(subject))
            {
                transcript.Insert(0, new ChatMessage(ChatRole.System, $"The subject of the customer's latest {turn.Channel.ConversationNoun} message: {subject}"));
            }

            // When a person is available, the transfer tool is offered for this turn, with guidance on when to use it. The
            // guidance is a leading system message, so it never reaches the should-respond judgement.
            if (handoffAvailable)
            {
                AttachTransferToAgentTool(context);

                var instructions = OmnichannelHandoffHelper.BuildHandoffInstructions(turn.FlowSettings);

                if (!string.IsNullOrEmpty(instructions))
                {
                    transcript.Insert(0, new ChatMessage(ChatRole.System, instructions));
                }
            }

            var deployment = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: generationToken)
                ?? throw new InvalidOperationException($"Unable to resolve a chat deployment for AI profile '{turn.Profile.ItemId}'.");

            _handoffTurn.Reset();

            using var usageScope = AIUsageScope.Begin(contextType: turn.Channel.UsageCategory);
            var completion = await _completionService.CompleteAsync(deployment, transcript, context, generationToken);

            reply = completion?.Messages?.FirstOrDefault()?.Text;
            outcome.HandoffRequested = handoffAvailable && _handoffTurn.HandoffRequested;
            outcome.HandoffReason = outcome.HandoffRequested ? _handoffTurn.Reason : null;
        }
        catch (Exception ex) when (generationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // A newer message superseded this turn mid-generation. The AI client may wrap the cancellation in its own
            // exception, so the token decides, not the exception type.
            throw new OperationCanceledException("The automated reply was superseded by a newer message.", ex, generationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The AI completion failed for Activity {ActivityId} with AI profile {ProfileId}.", turn.Activity.ItemId.SanitizeLogValue(), turn.Profile.ItemId.SanitizeLogValue());

            return null;
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            _logger.LogWarning("The AI completion returned no content for Activity {ActivityId}.", turn.Activity.ItemId.SanitizeLogValue());

            return null;
        }

        if (reply.Contains(HangupMarker, StringComparison.Ordinal))
        {
            outcome.HangupRequested = true;
            reply = reply.Replace(HangupMarker, string.Empty, StringComparison.Ordinal).Trim();
        }

        // The model ended with only a marker: the neutral bridge line for a handoff, or a neutral sign-off.
        if (string.IsNullOrWhiteSpace(reply))
        {
            reply = outcome.HandoffRequested
                ? S["Thanks! I'm connecting you with a specialist who will continue from here."].Value
                : S["Thanks for your time. Goodbye."].Value;
        }

        return reply;
    }

    private async Task<bool> StartConversationAsync(OmnichannelMessage message, OmnichannelChannelEndpoint endpoint, CancellationToken cancellationToken)
    {
        foreach (var starter in _conversationStarters)
        {
            if (await starter.TryStartAsync(message, endpoint, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    // The message the reply answers, for a channel that threads replies. A redelivered or recovered turn carries no
    // provider message of its own, so the newest stored message from the customer stands in.
    private async Task<OmnichannelMessage> ResolveReplyToAsync(IAutomatedMessagingChannel channel, OmnichannelMessage message, OmnichannelChannelEndpoint endpoint, string customerAddress, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(message.ProviderMessageId))
        {
            return message;
        }

        var channelName = channel.Channel;
        var serviceAddress = endpoint.Value;

        return await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.Channel == channelName && index.CustomerAddress == customerAddress && index.ServiceAddress == serviceAddress && index.IsInbound,
                collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken) ?? message;
    }

    private async Task<IReadOnlyList<AIChatSessionPrompt>> GetTranscriptAsync(AIChatSession chatSession)
        => (await _promptStore.GetPromptsAsync(chatSession.SessionId))
            .Where(prompt => !prompt.IsGeneratedPrompt)
            .ToList();

    private async Task<SubjectFlowSettings> FindFlowSettingsAsync(string subjectContentType, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(subjectContentType)
            ? null
            : await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(subjectContentType, cancellationToken);

    private async Task ApplyOptOutAsync(IAutomatedMessagingChannel channel, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        var contact = await GetContactAsync(_contentManager, activity);

        if (contact is not null)
        {
            channel.ApplyOptOut(contact, _clock.UtcNow);

            await _contentManager.UpdateAsync(contact);
        }

        activity.Status = ActivityStatus.Cancelled;

        if (string.IsNullOrWhiteSpace(activity.Notes))
        {
            activity.Notes = $"The automated {channel.ConversationNoun} was cancelled because the contact asked to opt out.";
        }

        await _activityStore.UpdateAsync(activity, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The contact of Activity {ActivityId} opted out of {Channel}; the automated conversation was cancelled.", activity.ItemId.SanitizeLogValue(), channel.Channel);
        }
    }

    // The transfer tool is attached to this one completion as a scoped system tool, because the automated conversation
    // calls the completion service directly and the profile's tool list skips system tools.
    private static void AttachTransferToAgentTool(AICompletionContext context)
    {
        var entry = new ToolRegistryEntry
        {
            Id = OmnichannelHandoffHelper.TransferToAgentToolName,
            Name = OmnichannelHandoffHelper.TransferToAgentToolName,
            Description = "Transfers the current conversation to a live human agent.",
            Source = ToolRegistryEntrySource.System,
            CreateAsync = serviceProvider => ValueTask.FromResult(serviceProvider.GetKeyedService<AITool>(OmnichannelHandoffHelper.TransferToAgentToolName)),
        };

        context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = new List<ToolRegistryEntry> { entry };
    }

    // A conversation an entry point's AI agent started can have no subject, and a message from an unknown sender no contact.
    private static async Task<ContentItem> GetSubjectAsync(IContentManager contentManager, OmnichannelActivity activity)
        => activity.Subject ?? (string.IsNullOrWhiteSpace(activity.SubjectContentType)
            ? null
            : await contentManager.NewAsync(activity.SubjectContentType));

    private static async Task<ContentItem> GetContactAsync(IContentManager contentManager, OmnichannelActivity activity)
        => string.IsNullOrWhiteSpace(activity.ContactContentItemId)
            ? null
            : await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

    private sealed record MessageDispatchResultView(bool Succeeded, string Error);
}
