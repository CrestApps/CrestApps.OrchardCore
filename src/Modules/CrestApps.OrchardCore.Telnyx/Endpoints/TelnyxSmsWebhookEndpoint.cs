using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Telnyx.Endpoints;

/// <summary>
/// The Telnyx messaging (SMS/MMS) webhook: it verifies the Telnyx Ed25519 signature, then routes an inbound
/// message onto the shared Omnichannel <c>SmsReceived</c> bus (which the SMS portal and the automated AI path
/// both observe) or applies an outbound delivery receipt to the sent message.
/// </summary>
internal static class TelnyxSmsWebhookEndpoint
{
    public const long MaximumRequestBodySizeBytes = 1024 * 1024;

    public static IEndpointRouteBuilder AddTelnyxSmsWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(TelnyxConstants.SmsWebhookPath, HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodySizeBytes));

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        IOptionsMonitor<TelnyxSmsOptions> optionsMonitor,
        IEnumerable<IOmnichannelEventHandler> handlers,
        YesSqlSession session,
        IClock clock,
        ILogger<TelnyxSmsProvider> logger)
    {
        var options = optionsMonitor.CurrentValue;

        if (!options.IsEnabled)
        {
            logger.LogWarning("Rejected a Telnyx SMS webhook because the Telnyx SMS provider is disabled.");

            return TypedResults.NotFound();
        }

        if (httpContext.Request.ContentLength is > MaximumRequestBodySizeBytes)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var read = await RequestBodyReader.ReadAsync(httpContext.Request, MaximumRequestBodySizeBytes, httpContext.RequestAborted);

        if (read.IsTooLarge)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = read.Body;

        if (string.IsNullOrEmpty(options.WebhookPublicKey))
        {
            logger.LogWarning("Rejected a Telnyx SMS webhook because no webhook public key is configured.");

            return TypedResults.Unauthorized();
        }

        var publicKey = options.WebhookPublicKey;
        var signature = httpContext.Request.Headers[TelnyxConstants.SignatureHeaderName].ToString();
        var timestamp = httpContext.Request.Headers[TelnyxConstants.TimestampHeaderName].ToString();

        if (!TelnyxWebhookSignatureValidator.TryValidate(publicKey, signature, timestamp, body))
        {
            logger.LogWarning("Rejected a Telnyx SMS webhook because the signature could not be validated.");

            return TypedResults.Unauthorized();
        }

        if (!TelnyxSmsWebhookParser.TryParse(body, out var messagingEvent))
        {
            logger.LogWarning("Ignored a Telnyx SMS webhook whose payload was not a recognized messaging event.");

            return TypedResults.Ok();
        }

        if (messagingEvent.IsInbound)
        {
            await HandleInboundAsync(httpContext, messagingEvent, handlers, session, clock, logger, httpContext.RequestAborted);
        }
        else
        {
            // Surface the carrier delivery outcome so an "accepted by Telnyx but never delivered" case (for example
            // an unregistered A2P/10DLC or unverified toll-free sender) is diagnosable from the logs.
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Telnyx SMS delivery receipt for message '{ProviderMessageId}': status '{Status}', error code '{ErrorCode}'.",
                    messagingEvent.ProviderMessageId,
                    messagingEvent.DeliveryStatus,
                    messagingEvent.ErrorCode);
            }

            // Delivery receipts update manual SMS conversations, whose tracking service ships with the SMS Portal
            // feature. Automated-only deployments do not enable it, so resolve it optionally and skip when absent —
            // the inbound path above never needs it, which is why this webhook must not hard-depend on it.
            var conversationService = httpContext.RequestServices.GetService<ISmsConversationService>();

            if (conversationService is not null)
            {
                await conversationService.ApplyDeliveryReceiptAsync(new SmsDeliveryReceipt
                {
                    ServiceAddress = messagingEvent.From,
                    ContactAddress = messagingEvent.To,
                    ProviderMessageId = messagingEvent.ProviderMessageId,
                    Status = messagingEvent.DeliveryStatus,
                    ErrorCode = messagingEvent.ErrorCode,
                }, httpContext.RequestAborted);
            }
        }

        return TypedResults.Ok();
    }

    private static async Task HandleInboundAsync(
        HttpContext httpContext,
        TelnyxSmsWebhookEvent messagingEvent,
        IEnumerable<IOmnichannelEventHandler> handlers,
        YesSqlSession session,
        IClock clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var message = new OmnichannelMessage
        {
            CustomerAddress = messagingEvent.From,
            ServiceAddress = messagingEvent.To,
            Content = messagingEvent.Text,
            Channel = OmnichannelConstants.Channels.Sms,
            CreatedUtc = clock.UtcNow,
            IsInbound = true,
            ProviderMessageId = messagingEvent.ProviderMessageId,
            MediaReferences = messagingEvent.MediaUrls.ToList(),
        };

        // Commit the delivery to the durable inbox first when it is available, keyed on the provider's own
        // message id: a Telnyx retry of the same text is then recognised as a duplicate and not stored or
        // answered twice, and processing that fails part-way is retried from storage rather than lost with this
        // request. A tenant without the inbox feature falls back to processing inline, as before.
        var inbox = httpContext.RequestServices.GetService<IProviderWebhookInbox>();

        if (inbox is not null && !string.IsNullOrEmpty(messagingEvent.ProviderMessageId))
        {
            var acceptance = await inbox.AcceptAsync(
                new ProviderWebhookInboxDelivery
                {
                    ProviderName = TelnyxConstants.ProviderTechnicalName,
                    DeliveryId = messagingEvent.ProviderMessageId,
                    HandlerName = SmsInboundInboxHandler.HandlerTechnicalName,
                    Payload = JsonSerializer.Serialize(message),
                },
                cancellationToken);

            if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Duplicate)
            {
                return;
            }

            if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Accepted)
            {
                try
                {
                    await inbox.DispatchAsync(acceptance.MessageId, cancellationToken);
                }
                catch (ConcurrencyException)
                {
                    // Another node claimed the same delivery. The inbox retries it from storage, so this request
                    // must not report a failure that would make the provider redeliver as well.
                }

                return;
            }
        }

        await session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        var omnichannelEvent = new OmnichannelEvent
        {
            Id = messagingEvent.ProviderMessageId,
            EventType = OmnichannelConstants.Events.SmsReceived,
            Subject = "SMS received",
            Data = BinaryData.FromString(messagingEvent.Text ?? string.Empty),
            Message = message,
        };

        await handlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), omnichannelEvent, logger);
    }
}
