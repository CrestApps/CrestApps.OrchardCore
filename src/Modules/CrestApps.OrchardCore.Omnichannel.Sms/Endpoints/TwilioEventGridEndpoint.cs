using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Twillio;
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
    /// <summary>
    /// Adds the twilio event grid endpoint.
    /// </summary>
    /// <param name="builder">The builder.</param>
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

        var site = await siteService.GetSiteSettingsAsync();

        if (!IsRequestValid(context, authToken, site.BaseUrl, logger))
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
            logger.LogInformation("Twilio message received.");
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

    internal static bool IsRequestValid(HttpContext context, string authToken, string siteBaseUrl, ILogger logger)
    {
        if (string.IsNullOrEmpty(authToken))
        {
            return false;
        }

        if (!context.Request.Headers.TryGetValue("X-Twilio-Signature", out var twilioSignature))
        {
            return false;
        }

        var form = context.Request.HasFormContentType
            ? context.Request.Form.ToDictionary(entry => entry.Key, entry => entry.Value.ToString())
            : [];

        var validator = new TwillioRequestValidator(authToken);

        var isValid = validator.Validate(GetExternalRequestUrl(context, siteBaseUrl), form, twilioSignature.ToString());

        if (!isValid)
        {
            logger.LogWarning("Twilio signature validation failed.");
        }

        return isValid;
    }

    /// <summary>
    /// Rebuilds the absolute URL that Twilio signed.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="siteBaseUrl">The configured public base URL of the site, when one is set.</param>
    private static string GetExternalRequestUrl(HttpContext context, string siteBaseUrl)
    {
        var request = context.Request;

        // Twilio signs the URL it was configured with, which is the site's public URL. Behind a TLS-terminating
        // proxy the request scheme, host and port are those of the internal hop, so signing them rejects genuine
        // deliveries. Prefer the operator-configured public base URL and fall back to the request only when unset.
        if (string.IsNullOrEmpty(siteBaseUrl) || !Uri.TryCreate(siteBaseUrl, UriKind.Absolute, out var siteBaseUri))
        {
            return $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}";
        }

        var authority = siteBaseUri.IsDefaultPort
            ? siteBaseUri.Host
            : $"{siteBaseUri.Host}:{siteBaseUri.Port}";

        var siteBasePath = siteBaseUri.AbsolutePath.TrimEnd('/');

        var localPath = $"{request.PathBase}{request.Path}";

        // The application may be hosted under a path base, and a proxy may or may not forward that prefix. Strip
        // whichever prefix is already present so the configured base path is applied exactly once instead of
        // being duplicated, which would sign a URL Twilio never called.
        localPath = TrimPrefix(localPath, request.PathBase.Value?.TrimEnd('/'));
        localPath = TrimPrefix(localPath, siteBasePath);

        return $"{siteBaseUri.Scheme}://{authority}{siteBasePath}{localPath}{request.QueryString}";
    }

    /// <summary>
    /// Removes a leading path segment from a request path when it is present.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <param name="prefix">The prefix to remove.</param>
    private static string TrimPrefix(string path, string prefix)
    {
        if (string.IsNullOrEmpty(prefix) || prefix == "/")
        {
            return path;
        }

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var remainder = path[prefix.Length..];

        // Only treat it as a prefix when it ends on a segment boundary, so "/tenant" never matches "/tenant-2".
        if (remainder.Length > 0 && remainder[0] != '/')
        {
            return path;
        }

        return remainder;
    }
}
