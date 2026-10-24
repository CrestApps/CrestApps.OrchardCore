using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;

/// <summary>
/// Reads Mailgun's webhooks: a JSON object with <c>signature</c> and <c>event-data</c>. A <c>failed</c> event is a hard
/// bounce when its severity is permanent and a soft bounce otherwise; an ESP block is a block. When the tenant has the
/// Mailgun signing key, the signature is checked.
/// </summary>
public sealed class MailgunDeliveryEventParser : IEmailDeliveryEventParser
{
    private readonly IEmailSecretProtector _secretProtector;
    private readonly IClock _clock;

    public MailgunDeliveryEventParser(
        IEmailSecretProtector secretProtector,
        IClock clock)
    {
        _secretProtector = secretProtector;
        _clock = clock;
    }

    public string Name => EmailChannelConstants.Providers.Mailgun;

    public async Task<EmailDeliveryEventParseResult> ParseAsync(HttpRequest request, EmailInboundSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var document = await DeliveryEventJson.ReadAsync(request.Body, cancellationToken);

        var root = document?.RootElement ?? default;
        var signingKey = _secretProtector.Unprotect(settings?.MailgunSigningKey);

        // The signature is checked before anything the caller sends can end the call early: a body that cannot be read
        // carries no valid signature either.
        if (!string.IsNullOrEmpty(signingKey) && !HasValidSignature(root, signingKey))
        {
            return EmailDeliveryEventParseResult.Unauthorized("The Mailgun signature is missing, wrong or too old.");
        }

        if (document is null)
        {
            return EmailDeliveryEventParseResult.Invalid("The Mailgun payload is not valid JSON.");
        }

        var deliveryEvent = Parse(DeliveryEventJson.GetObject(root, "event-data"));

        return deliveryEvent is null
            ? EmailDeliveryEventParseResult.Handled("The Mailgun event is not one the channel acts on.")
            : EmailDeliveryEventParseResult.Of([deliveryEvent]);
    }

    private bool HasValidSignature(JsonElement root, string signingKey)
    {
        var signature = DeliveryEventJson.GetObject(root, "signature");

        return MailgunInboundEmailParser.IsSignatureValid(
            signingKey,
            DeliveryEventJson.GetString(signature, "timestamp"),
            DeliveryEventJson.GetString(signature, "token"),
            DeliveryEventJson.GetString(signature, "signature"),
            _clock.UtcNow);
    }

    /// <summary>
    /// Reads one event.
    /// </summary>
    /// <param name="data">The <c>event-data</c> object.</param>
    /// <returns>The event, or <see langword="null"/> when the channel does not act on it.</returns>
    public static EmailDeliveryEvent Parse(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = DeliveryEventJson.GetString(data, "event")?.ToLowerInvariant();
        var reason = DeliveryEventJson.GetString(data, "reason")?.ToLowerInvariant();

        EmailDeliveryEventKind? kind = name switch
        {
            "failed" when reason == "espblock" => EmailDeliveryEventKind.Blocked,
            "failed" when string.Equals(DeliveryEventJson.GetString(data, "severity"), "permanent", StringComparison.OrdinalIgnoreCase) => EmailDeliveryEventKind.HardBounce,
            "failed" => EmailDeliveryEventKind.SoftBounce,
            "complained" => EmailDeliveryEventKind.Complaint,
            "unsubscribed" => EmailDeliveryEventKind.Unsubscribed,
            "delivered" => EmailDeliveryEventKind.Delivered,
            _ => null,
        };

        if (kind is null)
        {
            return null;
        }

        var status = DeliveryEventJson.GetObject(data, "delivery-status");
        var headers = DeliveryEventJson.GetObject(DeliveryEventJson.GetObject(data, "message"), "headers");

        return new EmailDeliveryEvent
        {
            Kind = kind.Value,
            Recipient = DeliveryEventJson.GetString(data, "recipient"),
            MessageId = DeliveryEventJson.GetString(headers, "message-id"),
            EventId = DeliveryEventJson.GetString(data, "id"),
            Status = DeliveryEventJson.GetString(status, "enhanced-code", "code"),
            Reason = DeliveryEventJson.GetString(status, "description", "message") ?? reason,
            OccurredUtc = DeliveryEventJson.GetTime(data, "timestamp"),
            Provider = EmailChannelConstants.Providers.Mailgun,
        };
    }
}
