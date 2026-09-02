using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Twillio;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Sms.Models;
using OrchardCore.Sms.Services;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Endpoints;

/// <summary>
/// The Twilio inbound-SMS webhook. Twilio POSTs an <c>application/x-www-form-urlencoded</c> body signed with an
/// <c>X-Twilio-Signature</c> HMAC over the request URL and parameters; this endpoint verifies that signature,
/// then raises an inbound <see cref="OmnichannelEvent"/> so the SMS is routed like any other channel event.
/// </summary>
internal static class TwilioWebhookEndpoint
{
    public static IEndpointRouteBuilder AddTwilioWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        // Provider webhooks follow the api/{provider}/webhook/{kind} convention (see the Telnyx SMS/voice webhooks).
        _ = builder.MapPost("api/twilio/webhook/sms", HandleAsync)
            .DisableAntiforgery()
            .AllowAnonymous();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IClock clock,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IShellHost shellHost,
        ShellSettings shellSettings,
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

        // Twilio signs the URL it was configured with, which is the site's public URL. Behind a TLS-terminating
        // proxy the raw request scheme/host/path belong to the internal hop, so validating those rejects genuine
        // deliveries; the validator honours the operator's configured public base URL instead.
        if (!IsRequestValid(context, authToken, site.BaseUrl, logger))
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
            // Log the Twilio MessageSid (the correlation key used throughout the flow) and the length, never the raw
            // phone numbers or body content, so the trace carries no customer PII.
            logger.LogInformation("Inbound Twilio SMS received. MessageSid: {MessageSid}, Length: {Length}.", messageSid, body?.Length ?? 0);
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

        var omnichannelEvent = new OmnichannelEvent()
        {
            Id = messageSid,
            EventType = OmnichannelConstants.Events.SmsReceived,
            Subject = $"SMS from {from}",
            Data = BinaryData.FromString(body),
            Message = omnichannelMessage,
        };

        // Generating the automated reply — a humanized settle pause, the AI completion and a "typing" delay — takes
        // tens of seconds. Running it inline would hold this webhook open well past Twilio's ~15s delivery timeout, so
        // Twilio would mark the delivery failed and RETRY it; the retries then race the original (and each other) for
        // the same conversation, producing SQLite "database is locked" and optimistic-concurrency failures and, to the
        // customer, mistimed or duplicate replies. Instead, acknowledge immediately and process the event in a fresh
        // shell scope off the request thread. The audit-record save is done there too, so THIS request performs no
        // database writes and cannot itself stall on write contention and return a 500 that Twilio would then retry.
        // The per-conversation lock and the single-active-generation registry in the handler still guarantee one
        // consolidated reply when several messages arrive close together.
        var backgroundScope = await shellHost.GetScopeAsync(shellSettings);

        _ = backgroundScope.UsingAsync(async scope =>
        {
            var scopedSession = scope.ServiceProvider.GetRequiredService<YesSqlSession>();
            var scopedHandlers = scope.ServiceProvider.GetServices<IOmnichannelEventHandler>();
            var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Startup>>();

            // Tag every log line produced while this inbound message is processed with its identifiers, so a single
            // customer's exchange can be followed end to end even when many conversations are interleaved in the log.
            using var logScope = scopedLogger.BeginScope(new Dictionary<string, object>
            {
                ["MessageSid"] = messageSid,
                ["Channel"] = channel,
            });

            try
            {
                await scopedSession.SaveAsync(omnichannelMessage, collection: OmnichannelConstants.CollectionName);

                await scopedHandlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), omnichannelEvent, scopedLogger);
            }
            catch (Exception ex)
            {
                scopedLogger.LogError(ex, "Failed to process inbound SMS event {MessageSid} in the background.", messageSid);
            }
        });

        // Return empty 200 OK to Twilio immediately.

        return TypedResults.Ok();
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
