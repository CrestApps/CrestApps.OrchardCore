using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Twillio;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Sms.Models;
using OrchardCore.Sms.Services;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Endpoints;

internal static class TwilioWebhookEndpoint
{
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
        IHostEnvironment hostEnvironment,
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

        var requestUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";

        Dictionary<string, string> parameters = null;

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);

            parameters = form.ToDictionary(p => p.Key, p => p.Value.ToString());
        }

        var validator = new TwillioRequestValidator(authToken);

        if (!request.Headers.TryGetValue("X-Twilio-Signature", out var signature) ||
            (hostEnvironment.IsProduction() && !validator.Validate(requestUrl, parameters, signature.First())))
        {
            logger.LogWarning("Unauthorized Twilio request.");

            return TypedResults.Forbid();
        }

        var data = await context.Request.ReadFormAsync();

        var from = data["From"].ToString();
        var to = data["To"].ToString();
        var body = data["Body"].ToString();
        var messageSid = data["MessageSid"].ToString();
        var channel = "SMS";

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Twilio message received from {From} to {To}, SID: {Sid}", from, to, messageSid);
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
