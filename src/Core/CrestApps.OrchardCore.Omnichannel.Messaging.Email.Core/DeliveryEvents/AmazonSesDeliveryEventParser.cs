using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;

/// <summary>
/// Reads Amazon SES bounce, complaint and delivery notifications delivered through Amazon SNS, from SES notifications
/// (<c>notificationType</c>) or configuration-set event publishing (<c>eventType</c>). The SNS signature and the
/// allowed topics are checked as for inbound email, and a subscription is confirmed by itself.
/// </summary>
public sealed class AmazonSesDeliveryEventParser : IEmailDeliveryEventParser
{
    private readonly AmazonSnsMessageVerifier _verifier;
    private readonly ILogger _logger;

    public AmazonSesDeliveryEventParser(
        AmazonSnsMessageVerifier verifier,
        ILogger<AmazonSesDeliveryEventParser> logger)
    {
        _verifier = verifier;
        _logger = logger;
    }

    public string Name => EmailChannelConstants.Providers.AmazonSes;

    public async Task<EmailDeliveryEventParseResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var document = await DeliveryEventJson.ReadAsync(request.Body, cancellationToken);

        if (document is null)
        {
            return EmailDeliveryEventParseResult.Invalid("The Amazon SNS payload is not valid JSON.");
        }

        var root = document.RootElement;
        var topicArn = DeliveryEventJson.GetString(root, "TopicArn");
        var allowedTopics = settings?.AmazonSnsTopicArns?.Where(topic => !string.IsNullOrWhiteSpace(topic)).ToArray() ?? [];

        if (allowedTopics.Length > 0 && !allowedTopics.Contains(topicArn, StringComparer.Ordinal))
        {
            return EmailDeliveryEventParseResult.Unauthorized("The Amazon SNS topic is not one the tenant allows.");
        }

        if (!await _verifier.VerifyAsync(root, cancellationToken))
        {
            return EmailDeliveryEventParseResult.Unauthorized("The Amazon SNS signature is missing or wrong.");
        }

        switch (DeliveryEventJson.GetString(root, "Type"))
        {
            case "SubscriptionConfirmation":
                var confirmed = await _verifier.ConfirmSubscriptionAsync(DeliveryEventJson.GetString(root, "SubscribeURL"), cancellationToken);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Amazon SNS asked to subscribe the email delivery events webhook to topic {TopicArn}; confirmed: {Confirmed}.", topicArn.SanitizeLogValue(), confirmed);
                }

                return EmailDeliveryEventParseResult.Handled("The Amazon SNS subscription was confirmed.");

            case "UnsubscribeConfirmation":
                return EmailDeliveryEventParseResult.Handled("Amazon SNS confirmed an unsubscription.");

            case "Notification":
                var message = DeliveryEventJson.GetString(root, "Message");

                if (string.IsNullOrEmpty(message))
                {
                    return EmailDeliveryEventParseResult.Invalid("The Amazon SNS notification carries no message.");
                }

                using (var notification = JsonDocument.Parse(message))
                {
                    var events = Parse(notification.RootElement);

                    return events.Count == 0
                        ? EmailDeliveryEventParseResult.Handled("The Amazon SES notification is not one the channel acts on.")
                        : EmailDeliveryEventParseResult.Of(events);
                }

            default:
                return EmailDeliveryEventParseResult.Invalid("The Amazon SNS message type is not one the webhook handles.");
        }
    }

    /// <summary>
    /// Reads the events of one SES notification.
    /// </summary>
    /// <param name="notification">The notification, the SNS message's body.</param>
    /// <returns>One event per recipient the notification names.</returns>
    public static IList<EmailDeliveryEvent> Parse(JsonElement notification)
    {
        var type = DeliveryEventJson.GetString(notification, "notificationType", "eventType");
        var mail = DeliveryEventJson.GetObject(notification, "mail");

        // The email's own Message-ID, when it carried one, matches what the business recorded; the SES id otherwise.
        var messageId = DeliveryEventJson.GetString(DeliveryEventJson.GetObject(mail, "commonHeaders"), "messageId") ??
            DeliveryEventJson.GetString(mail, "messageId");

        var events = new List<EmailDeliveryEvent>();

        switch (type)
        {
            case "Bounce":
                var bounce = DeliveryEventJson.GetObject(notification, "bounce");
                var bounceKind = string.Equals(DeliveryEventJson.GetString(bounce, "bounceType"), "Permanent", StringComparison.OrdinalIgnoreCase)
                    ? EmailDeliveryEventKind.HardBounce
                    : EmailDeliveryEventKind.SoftBounce;

                foreach (var (recipient, index) in DeliveryEventJson.GetArray(bounce, "bouncedRecipients").Select((value, index) => (value, index)))
                {
                    events.Add(new EmailDeliveryEvent
                    {
                        Kind = bounceKind,
                        Recipient = DeliveryEventJson.GetString(recipient, "emailAddress"),
                        MessageId = messageId,
                        EventId = DeliveryEventJson.GetString(bounce, "feedbackId") is { } feedbackId ? $"{feedbackId}:{index}" : null,
                        Status = DeliveryEventJson.GetString(recipient, "status"),
                        Reason = DeliveryEventJson.GetString(recipient, "diagnosticCode") ?? DeliveryEventJson.GetString(bounce, "bounceSubType"),
                        OccurredUtc = DeliveryEventJson.GetTime(bounce, "timestamp"),
                        Provider = EmailChannelConstants.Providers.AmazonSes,
                    });
                }

                break;

            case "Complaint":
                var complaint = DeliveryEventJson.GetObject(notification, "complaint");

                foreach (var (recipient, index) in DeliveryEventJson.GetArray(complaint, "complainedRecipients").Select((value, index) => (value, index)))
                {
                    events.Add(new EmailDeliveryEvent
                    {
                        Kind = EmailDeliveryEventKind.Complaint,
                        Recipient = DeliveryEventJson.GetString(recipient, "emailAddress"),
                        MessageId = messageId,
                        EventId = DeliveryEventJson.GetString(complaint, "feedbackId") is { } feedbackId ? $"{feedbackId}:{index}" : null,
                        Reason = DeliveryEventJson.GetString(complaint, "complaintFeedbackType"),
                        OccurredUtc = DeliveryEventJson.GetTime(complaint, "timestamp"),
                        Provider = EmailChannelConstants.Providers.AmazonSes,
                    });
                }

                break;

            case "DeliveryDelay":
                var delay = DeliveryEventJson.GetObject(notification, "deliveryDelay");

                foreach (var recipient in DeliveryEventJson.GetArray(delay, "delayedRecipients"))
                {
                    events.Add(new EmailDeliveryEvent
                    {
                        Kind = EmailDeliveryEventKind.Deferred,
                        Recipient = DeliveryEventJson.GetString(recipient, "emailAddress"),
                        MessageId = messageId,
                        Status = DeliveryEventJson.GetString(recipient, "status"),
                        Reason = DeliveryEventJson.GetString(recipient, "diagnosticCode") ?? DeliveryEventJson.GetString(delay, "delayType"),
                        OccurredUtc = DeliveryEventJson.GetTime(delay, "timestamp"),
                        Provider = EmailChannelConstants.Providers.AmazonSes,
                    });
                }

                break;
        }

        return events;
    }
}
