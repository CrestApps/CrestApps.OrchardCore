using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Re-drives the automated conversations, on every automated channel, whose last turn is a customer's message that was
/// never answered because the reply being composed was lost (a restart, a crash). Only a recent message is answered: a
/// thread the customer wrote to long ago is not woken with a late reply.
/// </summary>
public sealed class AutomatedOwedReplyRecovery
{
    /// <summary>
    /// How recent an unanswered message must be for its reply to be recovered.
    /// </summary>
    public static readonly TimeSpan MaxOwedReplyAge = TimeSpan.FromMinutes(30);

    private const int BatchSize = 100;
    private const int MaxConversationsPerPass = 200;

    private readonly IEnumerable<IAutomatedMessagingChannel> _channels;
    private readonly AutomatedConversationHandler _handler;
    private readonly IAutomatedConversationGate _conversationGate;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomatedOwedReplyRecovery"/> class.
    /// </summary>
    /// <param name="channels">The automated channels.</param>
    /// <param name="handler">The automated conversation handler the owed turn is re-driven through.</param>
    /// <param name="conversationGate">The registry of the replies being composed right now.</param>
    /// <param name="chatSessionManager">The AI chat session manager.</param>
    /// <param name="promptStore">The AI chat transcript store.</param>
    /// <param name="endpointManager">The manager of the business's addresses.</param>
    /// <param name="session">The session the activities are read from.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AutomatedOwedReplyRecovery(
        IEnumerable<IAutomatedMessagingChannel> channels,
        AutomatedConversationHandler handler,
        IAutomatedConversationGate conversationGate,
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IOmnichannelChannelEndpointManager endpointManager,
        ISession session,
        IClock clock,
        ILogger<AutomatedOwedReplyRecovery> logger)
    {
        _channels = channels;
        _handler = handler;
        _conversationGate = conversationGate;
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _endpointManager = endpointManager;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Re-drives the owed replies, until the deadline passes.
    /// </summary>
    /// <param name="deadlineUtc">When to stop, so the background task's lock does not expire mid-pass.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many conversations were re-driven.</returns>
    public async Task<int> RecoverAsync(DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        var channels = _channels.ToDictionary(channel => channel.Channel, StringComparer.OrdinalIgnoreCase);

        if (channels.Count == 0)
        {
            return 0;
        }

        var channelNames = channels.Keys.ToArray();
        var cutoff = _clock.UtcNow.Subtract(MaxOwedReplyAge);
        var documentId = 0L;
        var examined = 0;
        var recovered = 0;

        while (examined < MaxConversationsPerPass && _clock.UtcNow < deadlineUtc)
        {
            var activities = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                    index.Status == ActivityStatus.AwaitingCustomerAnswer &&
                    index.InteractionType == ActivityInteractionType.Automated &&
                    index.Channel.IsIn(channelNames) &&
                    index.DocumentId > documentId,
                    collection: OmnichannelConstants.CollectionName)
                .OrderBy(index => index.DocumentId)
                .Take(BatchSize)
                .ListAsync(cancellationToken);

            if (!activities.Any())
            {
                break;
            }

            foreach (var activity in activities)
            {
                if (_clock.UtcNow >= deadlineUtc)
                {
                    return recovered;
                }

                documentId = activity.Id;
                examined++;

                try
                {
                    if (channels.TryGetValue(activity.Channel, out var channel) &&
                        await TryRecoverAsync(channel, activity, cutoff, cancellationToken))
                    {
                        recovered++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Recovering the owed automated reply of Activity {ActivityId} failed.", activity.ItemId.SanitizeLogValue());
                }
            }
        }

        return recovered;
    }

    private async Task<bool> TryRecoverAsync(IAutomatedMessagingChannel channel, OmnichannelActivity activity, DateTime cutoff, CancellationToken cancellationToken)
    {
        // A reply being composed right now on this node is not lost. After a restart the registry is empty, so a stranded
        // conversation is not skipped here.
        if (string.IsNullOrWhiteSpace(activity.AISessionId) ||
            string.IsNullOrEmpty(activity.ChannelEndpointId) ||
            _conversationGate.IsGenerating(activity.AISessionId))
        {
            return false;
        }

        var chatSession = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (chatSession is null)
        {
            return false;
        }

        var last = (await _promptStore.GetPromptsAsync(chatSession.SessionId))
            .Where(prompt => !prompt.IsGeneratedPrompt)
            .LastOrDefault();

        // A reply is owed only when the last thing said was the customer's, and recently.
        if (last is null || last.Role != ChatRole.User || string.IsNullOrWhiteSpace(last.Content) || last.CreatedUtc < cutoff)
        {
            return false;
        }

        var endpoint = await _endpointManager.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

        if (endpoint is null || !endpoint.HasCapability(channel.Channel) || string.IsNullOrWhiteSpace(endpoint.Value))
        {
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Re-driving an owed automated {Channel} reply for Activity {ActivityId}.", channel.Channel, activity.ItemId.SanitizeLogValue());
        }

        // The customer's trailing message is raised again. The handler recognises it as the trailing turn and does not
        // store it twice, then sends the one owed reply under the same lock and generation registry as a live delivery.
        await _handler.HandleAsync(new OmnichannelEvent
        {
            EventType = channel.ReceivedEventType,
            Subject = "Owed automated reply",
            Data = BinaryData.FromString(last.Content),
            Message = new OmnichannelMessage
            {
                Channel = channel.Channel,
                CustomerAddress = activity.PreferredDestination,
                ServiceAddress = endpoint.Value,
                Content = last.Content,
                IsInbound = true,
            },
        }, cancellationToken);

        return true;
    }
}
