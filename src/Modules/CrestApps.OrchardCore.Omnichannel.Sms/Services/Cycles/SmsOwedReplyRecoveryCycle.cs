using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Omnichannel;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI;
using CrestApps.Core.Hosting.Background;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Handlers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// Recovers automated SMS conversations whose reply was owed but never sent.
/// </summary>
public sealed class SmsOwedReplyRecoveryCycle : ISmsOwedReplyRecoveryCycle
{
    private const int _leaseMilliseconds = 300_000;
    private const int _batchSize = 100;
    private const int _maxConversationsPerInvocation = 200;

    // A reply is only recovered when the customer's unanswered message is recent. This is the whole point of the
    // staleness bound: recovery exists to answer a reply that was mid-flight when the process stopped (picked up on
    // the next scheduled run, minutes later), NOT to resurrect a thread the customer sent to hours or days ago. Waking
    // an old, wound-down conversation with a late reply is worse than staying silent — the customer has moved on, and
    // the no-response timeout already governs those. The window comfortably covers a normal restart and a couple of
    // missed runs, while excluding anything genuinely stale.
    private const int _maxOwedReplyAgeMinutes = 30;

    private readonly ILogger _logger;
    private readonly ISession _session;
    private readonly TimeProvider _timeProvider;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly ICatalog<OmnichannelChannelEndpoint> _endpointCatalog;
    private readonly IAutomatedConversationGate _conversationGate;
    private readonly IEnumerable<IOmnichannelEventHandler> _eventHandlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsOwedReplyRecoveryCycle"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="session">The session.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="promptStore">The prompt store.</param>
    /// <param name="chatSessionManager">The chat session manager.</param>
    /// <param name="endpointCatalog">The endpoint catalog.</param>
    /// <param name="conversationGate">The conversation gate.</param>
    /// <param name="eventHandlers">The omnichannel event handlers, one of which owns the automated reply.</param>
    public SmsOwedReplyRecoveryCycle(
        ILogger<SmsOwedReplyRecoveryCycle> logger,
        ISession session,
        TimeProvider timeProvider,
        IAIChatSessionPromptStore promptStore,
        IAIChatSessionManager chatSessionManager,
        ICatalog<OmnichannelChannelEndpoint> endpointCatalog,
        IAutomatedConversationGate conversationGate,
        IEnumerable<IOmnichannelEventHandler> eventHandlers)
    {
        _logger = logger;
        _session = session;
        _timeProvider = timeProvider;
        _promptStore = promptStore;
        _chatSessionManager = chatSessionManager;
        _endpointCatalog = endpointCatalog;
        _conversationGate = conversationGate;
        _eventHandlers = eventHandlers;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Re-drive ONLY the automated AI handler. Invoking every IOmnichannelEventHandler would also hand the event to
        // the human SMS-portal inbound processor, which would try to route it as a workspace conversation — a message
        // that belongs to an automated activity is not a portal message.
        var handler = _eventHandlers
            .OfType<SmsOmnichannelEventHandler>()
            .FirstOrDefault();

        if (handler is null)
        {
            return;
        }

        // Stop before the lock lease can expire; the remainder is picked up on the next scheduled run. Each recovered
        // conversation runs a full reply generation (settle + AI + "typing"), so the budget is charged per item.
        var deadline = _timeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(_leaseMilliseconds * 0.6);

        // Only conversations whose unanswered customer message arrived after this moment are eligible.
        var owedReplyCutoff = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-_maxOwedReplyAgeMinutes);

        long documentId = 0;
        var processedCount = 0;

        while (processedCount < _maxConversationsPerInvocation && _timeProvider.GetUtcNow().UtcDateTime < deadline)
        {
            var activities = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(x =>
                    x.Status == ActivityStatus.AwaitingCustomerAnswer &&
                    x.InteractionType == ActivityInteractionType.Automated &&
                    x.Channel == OmnichannelConstants.Channels.Sms &&
                    x.DocumentId > documentId,
                    collection: OmnichannelConstants.CollectionName)
                .OrderBy(x => x.DocumentId)
                .Take(_batchSize)
                .ListAsync(cancellationToken);

            if (!activities.Any())
            {
                break;
            }

            foreach (var activity in activities)
            {
                if (_timeProvider.GetUtcNow().UtcDateTime >= deadline)
                {
                    return;
                }

                documentId = activity.Id;
                processedCount++;

                try
                {
                    await TryRecoverAsync(activity, _chatSessionManager, _promptStore, _endpointCatalog, handler, _conversationGate, owedReplyCutoff, _logger, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to recover the owed automated SMS reply for Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());
                }
            }
        }
    }

    private static async Task TryRecoverAsync(
        OmnichannelActivity activity,
        IAIChatSessionManager _chatSessionManager,
        IAIChatSessionPromptStore _promptStore,
        ICatalog<OmnichannelChannelEndpoint> _endpointCatalog,
        SmsOmnichannelEventHandler handler,
        IAutomatedConversationGate _conversationGate,
        DateTime owedReplyCutoff,
        ILogger _logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            return;
        }

        // A live inbound is already composing a reply for this conversation on this node; leave it alone. After a
        // restart the registry is empty, so genuinely stranded conversations are not skipped here.
        if (_conversationGate.IsGenerating(activity.AISessionId))
        {
            return;
        }

        var chatSession = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (chatSession is null)
        {
            return;
        }

        var prompts = (await _promptStore.GetPromptsAsync(chatSession.SessionId))
            .Where(x => !x.IsGeneratedPrompt)
            .ToList();

        // A reply is owed only when the last thing said was the customer's. If the last message is the assistant's,
        // the conversation was already answered and is legitimately waiting on the customer.
        var lastPrompt = prompts.LastOrDefault();

        if (lastPrompt is null || lastPrompt.Role != ChatRole.User || string.IsNullOrWhiteSpace(lastPrompt.Content))
        {
            return;
        }

        // Only answer a recently stranded message. An owed reply lost to a restart is recovered on the next scheduled
        // run (minutes later); a much older unanswered message belongs to a wound-down conversation that should not be
        // resurrected with a late reply. Prompts written before this task stamped CreatedUtc read as the default value,
        // which is safely treated as too old.
        if (lastPrompt.CreatedUtc < owedReplyCutoff)
        {
            return;
        }

        if (string.IsNullOrEmpty(activity.ChannelEndpointId))
        {
            return;
        }

        var endpoint = await _endpointCatalog.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

        if (endpoint is null ||
            !string.Equals(endpoint.Channel, activity.Channel, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(endpoint.Value))
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Re-driving an owed automated SMS reply for Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());
        }

        // Re-raise the customer's trailing message as an inbound event. The handler's idempotent store recognises the
        // message is already the trailing turn and does not duplicate it, then generates and sends the single owed
        // reply through the same per-conversation lock and generation registry as a live delivery.
        var omnichannelEvent = new OmnichannelEvent
        {
            EventType = OmnichannelConstants.Events.SmsReceived,
            Subject = $"SMS from {activity.PreferredDestination}",
            Data = BinaryData.FromString(lastPrompt.Content),
            Message = new OmnichannelMessage
            {
                Channel = OmnichannelConstants.Channels.Sms,
                CustomerAddress = activity.PreferredDestination,
                ServiceAddress = endpoint.Value,
                Content = lastPrompt.Content,
                IsInbound = true,
            },
        };

        await handler.HandleAsync(omnichannelEvent, cancellationToken);
    }
}
