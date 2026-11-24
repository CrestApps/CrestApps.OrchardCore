using System.Security.Cryptography;
using System.Text;
using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;

/// <summary>
/// Acts on what providers report about email after it left: suppresses addresses that bounce for good or keep
/// bouncing and people who report the mail as spam, marks the bounced message failed, pauses an address a receiving
/// system blocked, slows mail to a receiving domain that keeps deferring, and pauses an address whose sending health
/// has turned bad.
/// </summary>
public interface IEmailDeliveryEventProcessor
{
    /// <summary>
    /// Acts on events. An event recorded before is skipped, so a redelivered webhook call changes nothing.
    /// </summary>
    /// <param name="events">The events.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many events were acted on.</returns>
    Task<int> ProcessAsync(IEnumerable<EmailDeliveryEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IEmailDeliveryEventProcessor"/>.
/// </summary>
/// <remarks>
/// The event-id check and the record that follows it are not atomic, so the same event delivered at the same moment to
/// two app instances can be acted on twice. Every action is safe to repeat (a suppression or an opt-out that exists is
/// kept, a pause already running is not lengthened, an email's retries count once), so the cost is a second log line.
/// The event id cannot be made unique in the database instead: the log's send entries carry no event id, and SQL Server
/// lets a unique index hold only one empty value.
/// </remarks>
public sealed class EmailDeliveryEventProcessor : IEmailDeliveryEventProcessor
{
    private readonly IEmailDeliveryLog _log;
    private readonly IEmailSuppressionList _suppressions;
    private readonly IEmailSendingGovernor _governor;
    private readonly IEmailOptOutService _optOutService;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IMessagingConversationService _conversationService;
    private readonly IEmailSentMessageFinder _sentMessages;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public EmailDeliveryEventProcessor(
        IEmailDeliveryLog log,
        IEmailSuppressionList suppressions,
        IEmailSendingGovernor governor,
        IEmailOptOutService optOutService,
        IOmnichannelChannelEndpointManager endpointManager,
        IMessagingConversationService conversationService,
        IEmailSentMessageFinder sentMessages,
        IClock clock,
        ILogger<EmailDeliveryEventProcessor> logger)
    {
        _log = log;
        _suppressions = suppressions;
        _governor = governor;
        _optOutService = optOutService;
        _endpointManager = endpointManager;
        _conversationService = conversationService;
        _sentMessages = sentMessages;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> ProcessAsync(IEnumerable<EmailDeliveryEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        var processed = 0;

        foreach (var deliveryEvent in events)
        {
            if (deliveryEvent is not null && await ProcessAsync(deliveryEvent, cancellationToken))
            {
                processed++;
            }
        }

        return processed;
    }

    private async Task<bool> ProcessAsync(EmailDeliveryEvent deliveryEvent, CancellationToken cancellationToken)
    {
        var messageId = EmailSendingGovernor.TrimMessageId(deliveryEvent.MessageId);
        var recipient = OmnichannelEmailAddress.Normalize(deliveryEvent.Recipient);

        // A bounce report that does not name the recipient still names the email, and the email names its recipient.
        if (!OmnichannelEmailAddress.IsValid(recipient) && messageId is not null)
        {
            recipient = OmnichannelEmailAddress.Normalize((await FindOriginalAsync(messageId, cancellationToken))?.CustomerAddress);
        }

        if (!OmnichannelEmailAddress.IsValid(recipient))
        {
            return false;
        }
        var eventId = CreateEventId(deliveryEvent, recipient, messageId);

        if (await _log.HasEventAsync(eventId, cancellationToken))
        {
            return false;
        }

        var (kind, isRejectedEmail) = Reclassify(deliveryEvent);
        var detail = Describe(deliveryEvent);
        var addressId = await _log.FindSendingAddressIdAsync(messageId, recipient, cancellationToken);
        var domain = EmailSendingGovernor.GetDomain(recipient);

        await _log.RecordAsync(
            new EmailDeliveryLogEntry
            {
                AddressId = addressId,
                Kind = kind,
                Recipient = recipient,
                RecipientDomain = domain,
                MessageId = messageId,
                OccurredUtc = ClampOccurredUtc(deliveryEvent.OccurredUtc, _clock.UtcNow),
                Detail = detail,
                EventId = eventId,
            },
            cancellationToken);

        switch (kind)
        {
            case EmailDeliveryEventKind.HardBounce:
                await _suppressions.SuppressAsync(recipient, EmailSuppressionReason.HardBounce, detail, cancellationToken: cancellationToken);
                await MarkMessageFailedAsync(messageId, detail, cancellationToken);
                await CheckHealthAsync(addressId, cancellationToken);
                break;

            case EmailDeliveryEventKind.Rejected:
                // The receiving system refused this one email for what it is (its size, its content). The email failed,
                // but neither the recipient nor the receiving domain did anything that should hold other mail back.
                await MarkMessageFailedAsync(messageId, detail, cancellationToken);
                break;

            case EmailDeliveryEventKind.SoftBounce:
                // Counted by email, not by report: a provider reports each retry of one email, and one email bouncing
                // three times says less about the address than three emails bouncing.
                var bouncedEmails = await _log.CountMessagesForRecipientAsync(recipient, EmailDeliveryEventKind.SoftBounce, _clock.UtcNow.Subtract(EmailHealthPolicy.SoftBounceWindow), cancellationToken);

                if (bouncedEmails >= EmailHealthPolicy.SoftBouncesBeforeSuppression)
                {
                    await _suppressions.SuppressAsync(recipient, EmailSuppressionReason.RepeatedSoftBounces, detail, cancellationToken: cancellationToken);
                }

                await _governor.NoteDomainDeferralAsync(addressId, domain, cancellationToken);
                break;

            case EmailDeliveryEventKind.Deferred:
                await _governor.NoteDomainDeferralAsync(addressId, domain, cancellationToken);
                break;

            case EmailDeliveryEventKind.Blocked:
                if (!string.IsNullOrEmpty(addressId))
                {
                    await _governor.PauseAsync(addressId, EmailSendingPauseKind.Blocked, $"{deliveryEvent.Provider ?? "a provider"} reported the address's mail blocked: {detail}", cancellationToken);
                }

                break;

            case EmailDeliveryEventKind.Complaint:
                await _suppressions.SuppressAsync(recipient, EmailSuppressionReason.Complaint, detail, cancellationToken: cancellationToken);
                await _optOutService.OptOutAsync(recipient, cancellationToken);
                await CheckHealthAsync(addressId, cancellationToken);
                break;

            case EmailDeliveryEventKind.Unsubscribed:
                await _optOutService.OptOutAsync(recipient, cancellationToken);
                break;
        }

        if (kind is not (EmailDeliveryEventKind.Delivered or EmailDeliveryEventKind.Sent) && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Email delivery event {Kind} from {Provider} for address '{AddressId}': {Detail}",
                kind,
                (deliveryEvent.Provider ?? "unknown").SanitizeLogValue(),
                (addressId ?? "unknown").SanitizeLogValue(),
                detail.SanitizeLogValue());
        }

        return true;
    }

    // What a bounce report really is, read from its status code with the provider's words around it. Providers label
    // bounces by their own rules: a refusal of the sender (5.7.x) reported as a bounce is a block, and suppressing the
    // recipient for it would lose a good address; a temporary failure (4.x.x) the provider is still trying is a deferral,
    // and one it gave up on is a bounce of that one email, never a dead address; a full mailbox (5.2.2) may empty; and a
    // refusal of the one email (its size, its content) says nothing about the recipient or the sender. That last one is
    // logged as a rejected email, which neither counts toward suppression nor slows the domain.
    internal static (EmailDeliveryEventKind Kind, bool IsRejectedEmail) Reclassify(EmailDeliveryEvent deliveryEvent)
    {
        if (deliveryEvent.Kind is not (EmailDeliveryEventKind.HardBounce or EmailDeliveryEventKind.SoftBounce))
        {
            return (deliveryEvent.Kind, false);
        }

        var text = string.Join(" ", new[] { deliveryEvent.Status, deliveryEvent.Reason }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));

        if (EmailFailureClassifier.IsTemporaryStatus(deliveryEvent.Status, text))
        {
            return (deliveryEvent.Kind == EmailDeliveryEventKind.HardBounce ? EmailDeliveryEventKind.SoftBounce : EmailDeliveryEventKind.Deferred, false);
        }

        var failure = EmailFailureClassifier.ClassifyStatus(deliveryEvent.Status, text);

        // A report without an enhanced status code is read from its words; a hard bounce's words are a permanent answer.
        if (failure is null && !string.IsNullOrWhiteSpace(text))
        {
            failure = EmailFailureClassifier.Classify(
                deliveryEvent.Kind == EmailDeliveryEventKind.HardBounce ? OmnichannelConstants.MessagingErrorCodes.RecipientRejected : null,
                text);
        }

        return failure switch
        {
            EmailFailureKind.Blocked => (EmailDeliveryEventKind.Blocked, false),
            EmailFailureKind.Rejected => (EmailDeliveryEventKind.Rejected, true),

            // A permanent answer that may pass, such as a full mailbox, counts toward the recipient's repeated soft
            // bounces instead of suppressing it at once.
            EmailFailureKind.Temporary => (EmailDeliveryEventKind.SoftBounce, false),

            // A receiving system that asked the sender to slow down put the email off; it is noted against its domain.
            EmailFailureKind.Throttled => (EmailDeliveryEventKind.Deferred, false),
            _ => (deliveryEvent.Kind, false),
        };
    }

    // When the event happened, as the provider says, kept between the log's retention and now. A provider's clock (or a
    // forged call) can date an event in the future, and a future entry would count in every window and never be pruned;
    // one older than the retention would only be pruned at once. Only the logged time is clamped: the event id is built
    // from the time the provider sent, so a redelivered event is still recognised.
    internal static DateTime ClampOccurredUtc(DateTime? occurredUtc, DateTime utcNow)
    {
        if (occurredUtc is not { } occurred)
        {
            return utcNow;
        }

        var earliest = utcNow.Subtract(EmailHealthPolicy.DeliveryLogRetention);

        return occurred > utcNow
            ? utcNow
            : occurred < earliest
                ? earliest
                : occurred;
    }

    private async Task CheckHealthAsync(string addressId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(addressId))
        {
            return;
        }

        var address = await _endpointManager.FindByIdAsync(addressId, cancellationToken);

        if (address is null)
        {
            return;
        }

        var settings = address.TryGet<EmailAddressSettings>(out var stored) ? stored : new EmailAddressSettings();

        await _governor.CheckHealthAsync(address, settings, cancellationToken);
    }

    private async Task MarkMessageFailedAsync(string messageId, string detail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(messageId))
        {
            return;
        }

        var original = await FindOriginalAsync(messageId, cancellationToken);

        if (original is null)
        {
            return;
        }

        await _conversationService.ApplyDeliveryReceiptAsync(new MessageDeliveryReceipt
        {
            Channel = EmailChannelConstants.ChannelName,
            ServiceAddress = original.ServiceAddress,
            ContactAddress = original.CustomerAddress,
            ProviderMessageId = messageId,
            Status = MessageDeliveryStatus.Failed,
            ErrorCode = detail,
        }, cancellationToken);
    }

    private Task<OmnichannelMessage> FindOriginalAsync(string messageId, CancellationToken cancellationToken)
        => _sentMessages.FindAsync(messageId, cancellationToken);

    private static string Describe(EmailDeliveryEvent deliveryEvent)
    {
        var parts = new[] { deliveryEvent.Status, deliveryEvent.Reason }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim());
        var text = string.Join(" ", parts);

        return string.IsNullOrEmpty(text) ? deliveryEvent.Kind.ToString() : text;
    }

    // The provider's own event id when it gives one; otherwise a hash of what makes the event itself, so the same report
    // delivered twice is still recognised.
    internal static string CreateEventId(EmailDeliveryEvent deliveryEvent, string recipient, string messageId)
    {
        var provider = string.IsNullOrWhiteSpace(deliveryEvent.Provider) ? "email" : deliveryEvent.Provider.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(deliveryEvent.EventId))
        {
            var id = $"{provider}:{deliveryEvent.EventId.Trim()}";

            return id.Length <= 100 ? id : $"{provider}:{Hash(id)}";
        }

        var key = $"{provider}|{deliveryEvent.Kind}|{recipient}|{messageId}|{deliveryEvent.OccurredUtc?.ToString("O") ?? string.Empty}|{deliveryEvent.Status}";

        return $"{provider}:{Hash(key)}";
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..40].ToLowerInvariant();
}
