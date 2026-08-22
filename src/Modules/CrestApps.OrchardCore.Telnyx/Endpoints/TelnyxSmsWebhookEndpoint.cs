using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
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
        ISmsConversationService conversationService,
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
            await HandleInboundAsync(messagingEvent, handlers, session, clock, logger, httpContext.RequestAborted);
        }
        else
        {
            await conversationService.ApplyDeliveryReceiptAsync(new SmsDeliveryReceipt
            {
                ServiceAddress = messagingEvent.From,
                CustomerAddress = messagingEvent.To,
                ProviderMessageId = messagingEvent.ProviderMessageId,
                Status = messagingEvent.DeliveryStatus,
                ErrorCode = messagingEvent.ErrorCode,
            }, httpContext.RequestAborted);
        }

        return TypedResults.Ok();
    }

    private static async Task HandleInboundAsync(
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
