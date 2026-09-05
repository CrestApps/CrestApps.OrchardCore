using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The default <see cref="ISmsOutboundOutbox"/>. It re-attempts queued outbound messages whose retry time has
/// arrived, respecting a per-endpoint send budget so a backlog of retries cannot burst past what a carrier
/// permits one number to send, and marks a message failed only once its backoff schedule is exhausted.
/// </summary>
public sealed class SmsOutboundOutbox : ISmsOutboundOutbox
{
    private readonly ISmsDispatcher _dispatcher;
    private readonly ISmsRealTimeNotifier _notifier;
    private readonly ISmsConversationStore _conversationStore;
    private readonly SmsPortalOptions _options;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsOutboundOutbox"/> class.
    /// </summary>
    /// <param name="dispatcher">The provider dispatcher.</param>
    /// <param name="notifier">The real-time notifier that moves the bubble's state in the open thread.</param>
    /// <param name="conversationStore">The conversation store used to address the notification.</param>
    /// <param name="options">The workspace options carrying the batch size and per-endpoint budget.</param>
    /// <param name="session">The session the messages are read from and saved to.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SmsOutboundOutbox(
        ISmsDispatcher dispatcher,
        ISmsRealTimeNotifier notifier,
        ISmsConversationStore conversationStore,
        IOptions<SmsPortalOptions> options,
        ISession session,
        IClock clock,
        ILogger<SmsOutboundOutbox> logger)
    {
        _dispatcher = dispatcher;
        _notifier = notifier;
        _conversationStore = conversationStore;
        _options = options.Value;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var queued = SmsDeliveryStatus.Queued.ToString();
        var now = _clock.UtcNow;

        var candidates = await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.Channel == OmnichannelConstants.Channels.Sms && !index.IsInbound,
                collection: OmnichannelConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .Take(Math.Max(1, _options.OutboxBatchSize))
            .ListAsync(cancellationToken);

        var due = candidates
            .Where(message => string.Equals(message.DeliveryStatus, queued, StringComparison.Ordinal))
            .Select(message => (Message: message, State: message.TryGet<SmsOutboundDeliveryState>(out var state) ? state : null))
            .Where(entry => entry.State?.NextAttemptUtc is not null && entry.State.NextAttemptUtc <= now)
            .ToArray();

        if (due.Length == 0)
        {
            return 0;
        }

        // A per-endpoint budget applied across the pass, so a backlog on one number cannot burst past the rate
        // the carrier permits and get that number blocked.
        var perEndpointBudget = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var maxPerEndpoint = Math.Max(1, _options.MaxMessagesPerPassPerEndpoint);
        var accepted = 0;
        var attempted = 0;

        foreach (var (message, state) in due)
        {
            var endpoint = message.ServiceAddress ?? string.Empty;

            perEndpointBudget.TryGetValue(endpoint, out var used);

            if (used >= maxPerEndpoint)
            {
                continue;
            }

            perEndpointBudget[endpoint] = used + 1;
            attempted++;

            var dispatch = await _dispatcher.SendAsync(
                new SmsMessage
                {
                    From = message.ServiceAddress,
                    To = message.CustomerAddress,
                    Body = message.Content,
                },
                cancellationToken);

            state.Attempts += 1;

            if (dispatch.Succeeded)
            {
                state.NextAttemptUtc = null;
                state.LastError = null;

                message.DeliveryStatus = SmsDeliveryStatus.Sent.ToString();
                message.ProviderMessageId = dispatch.ProviderMessageId;
                message.ErrorCode = null;
                accepted++;
            }
            else
            {
                state.LastError = dispatch.GetErrorText();

                if (SmsOutboundDeliveryState.CanRetry(state.Attempts))
                {
                    state.NextAttemptUtc = now.Add(SmsOutboundDeliveryState.GetDelay(state.Attempts));
                }
                else
                {
                    state.NextAttemptUtc = null;
                    message.DeliveryStatus = SmsDeliveryStatus.Failed.ToString();
                }

                message.ErrorCode = state.LastError;
            }

            message.Put(state);

            await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

            await NotifyAsync(message, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The SMS outbox re-attempted {Attempted} queued messages and {Accepted} were accepted.",
                attempted,
                accepted);
        }

        return accepted;
    }

    private async Task NotifyAsync(OmnichannelMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message.ConversationId))
        {
            return;
        }

        var conversation = await _conversationStore.FindByIdAsync(message.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        await _notifier.MessageDeliveryUpdatedAsync(
            new SmsDeliveryNotification
            {
                ConversationId = conversation.ItemId,
                MessageId = message.Id,
                Status = Enum.TryParse<SmsDeliveryStatus>(message.DeliveryStatus, out var status) ? status : SmsDeliveryStatus.Queued,
                ErrorCode = message.ErrorCode,
                AssignedAgentId = conversation.AssignedAgentId,
                OwnerQueueId = conversation.OwnerType == SmsConversationOwnerType.Queue ? conversation.OwnerId : null,
            },
            cancellationToken);
    }
}
