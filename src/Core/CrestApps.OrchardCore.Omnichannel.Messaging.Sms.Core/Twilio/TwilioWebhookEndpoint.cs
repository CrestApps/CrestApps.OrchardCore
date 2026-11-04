using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
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
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Twilio;

/// <summary>
/// The Twilio inbound-SMS webhook. Twilio POSTs an <c>application/x-www-form-urlencoded</c> body signed with an
/// <c>X-Twilio-Signature</c> HMAC over the request URL and parameters; this endpoint verifies that signature,
/// then commits the text to the durable provider webhook inbox, which raises the inbound SMS event from storage.
/// </summary>
internal static class TwilioWebhookEndpoint
{
    /// <summary>
    /// The path Twilio posts inbound texts to, relative to the tenant, shown to the operator on the SMS settings screen.
    /// </summary>
    public const string SmsWebhookPath = "api/twilio/webhook/sms";

    public static IEndpointRouteBuilder AddTwilioWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        // Provider webhooks follow the api/{provider}/webhook/{kind} convention (see the Telnyx SMS/voice webhooks).
        _ = builder.MapPost(SmsWebhookPath, HandleAsync)
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
        ILogger<TwilioSmsWebhook> logger)
    {
        var settings = await siteService.GetSettingsAsync<TwilioSettings>();

        // Twilio is a provider of Orchard Core's SMS feature, switched on in the SMS settings rather than enabled as a
        // feature, so the route exists on every tenant with SMS; it answers only while the provider is on.
        if (!settings.IsEnabled)
        {
            logger.LogWarning("A Twilio inbound-SMS webhook arrived while the Twilio SMS provider is disabled; it was refused.");

            return TypedResults.NotFound();
        }

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

        if (logger.IsEnabled(LogLevel.Information))
        {
            // Log the Twilio MessageSid (the correlation key used throughout the flow) and the length, never the raw
            // phone numbers or body content, so the trace carries no customer PII.
            logger.LogInformation("Inbound Twilio SMS received. MessageSid: {MessageSid}, Length: {Length}.", messageSid.SanitizeLogValue(), body?.Length ?? 0);
        }

        var omnichannelMessage = new OmnichannelMessage
        {
            CustomerAddress = from,
            ServiceAddress = to,
            Content = body,
            Channel = OmnichannelConstants.Channels.Sms,
            CreatedUtc = clock.UtcNow,
            IsInbound = true,
            MediaReferences = ReadMediaUrls(form),

            // Twilio's MessageSid stays the same on every redelivery, so it is the idempotency key of the durable inbox.
            ProviderMessageId = messageSid,
        };

        return await AcceptInboundAsync(context, omnichannelMessage, shellHost, shellSettings, logger);
    }

    /// <summary>
    /// Commits an authenticated inbound text to the durable provider webhook inbox, keyed on Twilio's MessageSid,
    /// before Twilio is answered, and then starts processing it once the response is sent.
    /// </summary>
    /// <remarks>
    /// Generating the automated reply (a humanized settle pause, the AI completion and a "typing" delay) takes tens
    /// of seconds, well past Twilio's ~15s delivery timeout, so the text is not processed inline. It is not processed
    /// in a discarded background task either: that lost the text on a restart and relied on an in-memory claim to
    /// recognise a redelivery, which another node never saw. The inbox stores the delivery before the answer, absorbs
    /// a redelivery of the same MessageSid on any node, and retries processing from storage when it fails part-way.
    /// </remarks>
    /// <param name="context">The current request.</param>
    /// <param name="message">The parsed inbound text, with its provider message id set.</param>
    /// <param name="shellHost">The shell host used to process the text after the response.</param>
    /// <param name="shellSettings">The current tenant's settings.</param>
    /// <param name="logger">The logger.</param>
    internal static async Task<IResult> AcceptInboundAsync(
        HttpContext context,
        OmnichannelMessage message,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger logger)
    {
        if (string.IsNullOrEmpty(message.ProviderMessageId))
        {
            logger.LogWarning("Rejected a Twilio inbound-SMS webhook that carried no MessageSid.");

            return TypedResults.BadRequest();
        }

        var inbox = context.RequestServices.GetService<IProviderWebhookInbox>();

        if (inbox is null)
        {
            // Every feature that maps this route depends on the provider webhook inbox, so this is a broken
            // registration. Asking Twilio to send the text again is better than dropping it.
            logger.LogError("The provider webhook inbox is not registered, so the inbound Twilio SMS {MessageSid} could not be stored.", message.ProviderMessageId.SanitizeLogValue());

            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        ProviderWebhookInboxAcceptanceResult acceptance;

        try
        {
            acceptance = await inbox.AcceptAsync(
                new ProviderWebhookInboxDelivery
                {
                    ProviderName = TwilioSmsProvider.TechnicalName,
                    DeliveryId = message.ProviderMessageId,
                    HandlerName = SmsInboundInboxHandler.HandlerTechnicalName,
                    Payload = JsonSerializer.Serialize(message),
                },
                context.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing was stored, so nothing picks this up later: this is the one case where Twilio must redeliver.
            logger.LogError(exception, "The inbound Twilio SMS {MessageSid} could not be stored in the provider webhook inbox.", message.ProviderMessageId.SanitizeLogValue());

            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Duplicate)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Ignoring a duplicate inbound Twilio SMS delivery for MessageSid {MessageSid}.", message.ProviderMessageId.SanitizeLogValue());
            }

            return TypedResults.Ok();
        }

        if (acceptance.Status != ProviderWebhookInboxAcceptanceStatus.Accepted)
        {
            // Busy: the delivery was not stored, so Twilio has to send it again.
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var messageId = acceptance.MessageId;

        AfterResponseWork.Schedule(
            context,
            shellHost,
            shellSettings,
            scope => DispatchAsync(scope, messageId),
            logger);

        return TypedResults.Ok();
    }

    private static async Task DispatchAsync(ShellScope scope, string messageId)
    {
        var inbox = scope.ServiceProvider.GetRequiredService<IProviderWebhookInbox>();

        try
        {
            await inbox.DispatchAsync(messageId);
        }
        catch (ConcurrencyException)
        {
            // Another node claimed the same delivery. The inbox retries it from storage.
        }
    }

    // A picture message (MMS) lists its media as NumMedia plus MediaUrl0..MediaUrlN-1. The messaging workspace copies
    // them into its own store when it receives the message.
    internal static List<string> ReadMediaUrls(IFormCollection form)
    {
        if (form is null || !int.TryParse(form["NumMedia"].ToString(), out var count) || count <= 0)
        {
            return [];
        }

        var urls = new List<string>();

        for (var index = 0; index < Math.Min(count, 10); index++)
        {
            var url = form[$"MediaUrl{index}"].ToString();

            if (!string.IsNullOrWhiteSpace(url))
            {
                urls.Add(url);
            }
        }

        return urls;
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
