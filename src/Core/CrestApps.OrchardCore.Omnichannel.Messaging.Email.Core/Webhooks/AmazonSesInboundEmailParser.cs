using System.Text;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

/// <summary>
/// Reads Amazon SES receiving, delivered by an SES receipt rule's SNS action: the SNS message is verified as signed by
/// Amazon SNS, a subscription confirmation is confirmed, and a notification's raw email (the action's <c>content</c>,
/// which needs the action's encoding set to UTF-8 or Base64) is parsed. When the tenant lists SNS topics, only those
/// topics are accepted.
/// </summary>
public sealed class AmazonSesInboundEmailParser : IInboundEmailWebhookParser
{
    private readonly AmazonSnsMessageVerifier _verifier;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmazonSesInboundEmailParser"/> class.
    /// </summary>
    /// <param name="verifier">The SNS signature verifier.</param>
    /// <param name="logger">The logger.</param>
    public AmazonSesInboundEmailParser(
        AmazonSnsMessageVerifier verifier,
        ILogger<AmazonSesInboundEmailParser> logger)
    {
        _verifier = verifier;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => EmailChannelConstants.Providers.AmazonSes;

    /// <inheritdoc/>
    public async Task<InboundEmailWebhookResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        JsonDocument document;

        try
        {
            // SNS posts JSON with a text/plain content type, so the body is read whatever the content type says.
            document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return InboundEmailWebhookResult.Invalid("The Amazon SNS payload is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            var topicArn = GetString(root, "TopicArn");

            var allowedTopics = settings?.AmazonSnsTopicArns?.Where(topic => !string.IsNullOrWhiteSpace(topic)).ToArray() ?? [];

            if (allowedTopics.Length > 0 && !allowedTopics.Contains(topicArn, StringComparer.Ordinal))
            {
                return InboundEmailWebhookResult.Unauthorized("The Amazon SNS topic is not one the tenant allows.");
            }

            if (!await _verifier.VerifyAsync(root, cancellationToken))
            {
                return InboundEmailWebhookResult.Unauthorized("The Amazon SNS signature is missing or wrong.");
            }

            switch (GetString(root, "Type"))
            {
                case "SubscriptionConfirmation":
                    var confirmed = await _verifier.ConfirmSubscriptionAsync(GetString(root, "SubscribeURL"), cancellationToken);

                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Amazon SNS asked to subscribe the inbound email webhook to topic {TopicArn}; confirmed: {Confirmed}.", topicArn.SanitizeLogValue(), confirmed);
                    }

                    return InboundEmailWebhookResult.Handled("The Amazon SNS subscription was confirmed.");

                case "UnsubscribeConfirmation":
                    return InboundEmailWebhookResult.Handled("Amazon SNS confirmed an unsubscription.");

                case "Notification":
                    return await ParseNotificationAsync(GetString(root, "Message"), cancellationToken);

                default:
                    return InboundEmailWebhookResult.Invalid("The Amazon SNS message type is not one the webhook handles.");
            }
        }
    }

    private static async Task<InboundEmailWebhookResult> ParseNotificationAsync(string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message))
        {
            return InboundEmailWebhookResult.Invalid("The Amazon SNS notification carries no message.");
        }

        using var notification = JsonDocument.Parse(message);
        var root = notification.RootElement;

        if (GetString(root, "notificationType") != "Received")
        {
            return InboundEmailWebhookResult.Handled("The Amazon SES notification is not a received email.");
        }

        var content = GetString(root, "content");

        if (string.IsNullOrEmpty(content))
        {
            return InboundEmailWebhookResult.Invalid("The Amazon SES notification has no email content. Set the SNS action's encoding to UTF-8 or Base64 so the email is included.");
        }

        var encoding = root.TryGetProperty("receipt", out var receipt) &&
            receipt.TryGetProperty("action", out var action)
            ? GetString(action, "encoding")
            : null;

        var bytes = string.Equals(encoding, "BASE64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(content)
            : Encoding.UTF8.GetBytes(content);

        using var stream = new MemoryStream(bytes);
        var email = await MimeInboundEmailParser.ParseAsync(stream, cancellationToken);

        if (receipt.ValueKind == JsonValueKind.Object &&
            receipt.TryGetProperty("recipients", out var recipients) &&
            recipients.ValueKind == JsonValueKind.Array)
        {
            foreach (var recipient in recipients.EnumerateArray().Select(item => new InboundEmailAddress(item.GetString()).Address))
            {
                if (recipient is not null && !email.DeliveredTo.Contains(recipient, StringComparer.Ordinal))
                {
                    email.DeliveredTo.Add(recipient);
                }
            }
        }

        return InboundEmailWebhookResult.Of(email);
    }

    private static string GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
