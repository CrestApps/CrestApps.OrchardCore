using System.Text;
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

internal static class TwilioEventGridEndpoint
{
    public static IEndpointRouteBuilder AddTwilioEventGridEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("Omnichannel/webhook/TwilioEventGrid", HandleAsync)
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

        if (!IsRequestValid(context, authToken, logger))
        {
            logger.LogWarning("Unauthorized Twilio request.");

            return TypedResults.Unauthorized();
        }

        var data = await context.Request.ReadFormAsync();

        var from = data["From"].ToString();
        var to = data["To"].ToString();
        var body = data["Body"].ToString();
        var messageSid = data["MessageSid"].ToString();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Twilio message received from {From} to {To}, SID: {Sid}",
                SanitizeLogValue(from), SanitizeLogValue(to), SanitizeLogValue(messageSid));
        }

        // Map to OmnichannelMessage
        var omnichannelMessage = new OmnichannelMessage
        {
            CustomerAddress = from,
            ServiceAddress = to,
            Content = body,
            Channel = "SMS",
            CreatedUtc = clock.UtcNow,
            IsInbound = true,
        };

        await session.SaveAsync(omnichannelMessage, collection: OmnichannelConstants.CollectionName);

        var omnichannelEvent = new OmnichannelEvent
        {
            Id = messageSid,
            EventType = OmnichannelConstants.Events.SmsReceived, // Event type constant
            Subject = $"SMS from {from}",
            Data = BinaryData.FromString(System.Text.Json.JsonSerializer.Serialize(data.ToDictionary(k => k.Key, v => v.Value.ToString()))),              // Store full Twilio payload
            Message = omnichannelMessage
        };

        // Invoke all registered event handlers
        await handlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), omnichannelEvent, logger);

        return TypedResults.Ok(); // Twilio expects 200 OK
    }

    private static bool IsRequestValid(HttpContext context, string authToken, ILogger logger)
    {
        if (string.IsNullOrEmpty(authToken))
        {
            return false;
        }

        if (!context.Request.Headers.TryGetValue("X-Twilio-Signature", out var twilioSignature))
        {
            return false;
        }

        var requestUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";

        var form = context.Request.HasFormContentType
            ? context.Request.Form.ToDictionary(k => k.Key, v => v.Value.ToString())
            : [];

        // Build string to sign
        var sb = new StringBuilder();
        sb.Append(requestUrl);

        foreach (var key in form.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            sb.Append(key).Append(form[key]);
        }

        var encoding = new UTF8Encoding();

        // HMAC-SHA1 required by Twilio
#pragma warning disable CA5350
        using var hmac = new System.Security.Cryptography.HMACSHA1(encoding.GetBytes(authToken));
#pragma warning restore CA5350
        var hash = hmac.ComputeHash(encoding.GetBytes(sb.ToString()));
        var signature = Convert.ToBase64String(hash);

        var isValid = signature == twilioSignature;

        if (!isValid)
        {
            logger.LogWarning("Twilio signature validation failed.");
        }

        return isValid;
    }

    private static string SanitizeLogValue(string value)
        => value.Replace("\r", "").Replace("\n", "");
}
