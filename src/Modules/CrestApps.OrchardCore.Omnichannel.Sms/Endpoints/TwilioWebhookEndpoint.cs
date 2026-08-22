using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Sms.Models;
using OrchardCore.Sms.Services;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Endpoints;

internal static class TwilioWebhookEndpoint
{
    /// <summary>
    /// Adds the twilio webhook endpoint.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static IEndpointRouteBuilder AddTwilioWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("Omnichannel/webhook/Twilio", HandleAsync)
            .DisableAntiforgery()
            .AllowAnonymous();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IEnumerable<IOmnichannelEventHandler> handlers,
        YesSqlSession session,
        IClock clock,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<Startup> logger)
    {
        var settings = await siteService.GetSettingsAsync<TwilioSettings>();

        var protector = dataProtectionProvider.CreateProtector(TwilioSmsProvider.ProtectorName);

        var authToken = string.IsNullOrEmpty(settings.AuthToken)
        ? null
        : protector.Unprotect(settings.AuthToken);

        if (string.IsNullOrEmpty(authToken))
        {
            logger.LogWarning("Twillio provider is missing the AuthToken.");

            return TypedResults.BadRequest();
        }

        var request = context.Request;

        var form = request.HasFormContentType
            ? await request.ReadFormAsync(context.RequestAborted)
            : null;

        var site = await siteService.GetSiteSettingsAsync();

        // Reuse the Event Grid endpoint's tested signature validator so both inbound paths honour the operator's
        // configured public base URL and path base. Building the signed URL from the raw request scheme/host/path
        // (as this endpoint previously did) omits the path base and rejects genuine deliveries behind a
        // TLS-terminating proxy.
        if (!TwilioEventGridEndpoint.IsRequestValid(context, authToken, site.BaseUrl, logger))
        {
            logger.LogWarning("Unauthorized Twilio request.");

            return TypedResults.Forbid();
        }

        form ??= await request.ReadFormAsync(context.RequestAborted);

        var from = form["From"].ToString();
        var to = form["To"].ToString();
        var body = form["Body"].ToString();
        var messageSid = form["MessageSid"].ToString();
        var channel = "SMS";

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Twilio message received.");
        }

        var omnichannelMessage = new OmnichannelMessage
        {
            CustomerAddress = from,
            ServiceAddress = to,
            Content = body,
            Channel = channel,
            CreatedUtc = clock.UtcNow,
            IsInbound = true,
        };

        await session.SaveAsync(omnichannelMessage, collection: OmnichannelConstants.CollectionName);

        var omnichannelEvent = new OmnichannelEvent()
        {
            Id = messageSid,
            EventType = OmnichannelConstants.Events.SmsReceived,
            Subject = $"SMS from {from}",
            Data = BinaryData.FromString(body),
            Message = omnichannelMessage,
        };

        await handlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), omnichannelEvent, logger);

        // Return empty 200 OK to Twilio

        return TypedResults.Ok();
    }
}
