using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Endpoints;

/// <summary>
/// The delivery events webhook: providers post bounces, spam complaints, blocks and deferrals here, guarded by the site's
/// webhook key. A batch of events is committed to the durable provider inbox and processed after the call is answered,
/// so a provider posting a thousand events at once never times out, and a redelivered batch is recognised.
/// </summary>
internal static class EmailDeliveryEventsWebhookEndpoint
{
    private const string InboxProviderName = "email-events";

    private const long MaxBodyBytes = 5L * 1024 * 1024;

    public static IEndpointRouteBuilder AddEmailDeliveryEventsWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost(EmailChannelConstants.DeliveryEventsRoute, HandleAsync)
            .DisableAntiforgery()
            .AllowAnonymous();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        string provider,
        HttpContext httpContext,
        ISiteService siteService,
        IEmailSecretProtector secretProtector,
        IEnumerable<IEmailDeliveryEventParser> parsers,
        IEnumerable<IProviderWebhookInbox> inboxes,
        IEmailDeliveryEventProcessor processor,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<EmailDeliveryEventProcessor> logger)
    {
        var settings = await siteService.GetSettingsAsync<EmailInboundSettings>();
        var keyMatches = EmailWebhookKey.Check(httpContext.Request, settings, secretProtector);

        if (keyMatches is null)
        {
            return TypedResults.NotFound();
        }

        if (keyMatches == false)
        {
            logger.LogWarning("An email delivery events webhook call for provider {Provider} carried a wrong or missing key and was refused.", provider.SanitizeLogValue());

            return TypedResults.Unauthorized();
        }

        var parser = parsers.FirstOrDefault(candidate => string.Equals(candidate.Name, provider, StringComparison.OrdinalIgnoreCase));

        if (parser is null)
        {
            return TypedResults.NotFound();
        }

        var sizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = MaxBodyBytes;
        }

        EmailDeliveryEventParseResult parsed;

        try
        {
            parsed = await parser.ParseAsync(httpContext.Request, settings, httpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "An email delivery events webhook call for provider {Provider} could not be read.", provider.SanitizeLogValue());

            return TypedResults.BadRequest();
        }

        if (parsed.IsUnauthorized)
        {
            logger.LogWarning("An email delivery events webhook call for provider {Provider} was refused: {Reason}", provider.SanitizeLogValue(), parsed.Reason.SanitizeLogValue());

            return TypedResults.Unauthorized();
        }

        if (parsed.IsInvalid)
        {
            logger.LogWarning("An email delivery events webhook call for provider {Provider} was not valid: {Reason}", provider.SanitizeLogValue(), parsed.Reason.SanitizeLogValue());

            return TypedResults.BadRequest();
        }

        if (parsed.Events.Count == 0)
        {
            return TypedResults.Ok();
        }

        var payload = JsonSerializer.Serialize(parsed.Events);
        var inbox = inboxes.FirstOrDefault();

        // Without the durable inbox the events are acted on now; each is recorded under its event id, so a provider
        // retrying the call changes nothing twice.
        if (inbox is null)
        {
            await processor.ProcessAsync(parsed.Events, httpContext.RequestAborted);

            return TypedResults.Ok();
        }

        var acceptance = await inbox.AcceptAsync(
            new ProviderWebhookInboxDelivery
            {
                ProviderName = InboxProviderName,
                DeliveryId = $"{parser.Name}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..48]}",
                HandlerName = EmailChannelConstants.DeliveryEventsInboxHandlerName,
                Payload = payload,
            },
            httpContext.RequestAborted);

        switch (acceptance.Status)
        {
            case ProviderWebhookInboxAcceptanceStatus.Busy:
                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);

            case ProviderWebhookInboxAcceptanceStatus.Duplicate:
                return TypedResults.Ok();
        }

        await EmailInboxBackgroundDispatch.DispatchAsync(shellHost, shellSettings, [acceptance.MessageId]);

        return TypedResults.Ok();
    }
}
