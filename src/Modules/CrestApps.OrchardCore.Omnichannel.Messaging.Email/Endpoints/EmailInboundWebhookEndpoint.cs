using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Endpoints;

/// <summary>
/// The inbound email webhook, <c>POST api/omnichannel/email/inbound/{provider}</c>. Every call must carry the tenant's
/// webhook key; the provider's parser then checks the provider's own signature when it signs its calls. Each email is
/// committed to the durable provider inbox before the call is answered, and processed after it, so a provider never
/// times out waiting for the workspace or the AI, and an email is never lost with a dropped request.
/// </summary>
internal static class EmailInboundWebhookEndpoint
{
    public static IEndpointRouteBuilder AddEmailInboundWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost(EmailChannelConstants.InboundWebhookRoute, HandleAsync)
            .DisableAntiforgery()
            .AllowAnonymous();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        string provider,
        HttpContext httpContext,
        ISiteService siteService,
        IEmailSecretProtector secretProtector,
        IEnumerable<IInboundEmailWebhookParser> parsers,
        IEmailInboundReceiver receiver,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<EmailInboundReceiver> logger)
    {
        var settings = await siteService.GetSettingsAsync<EmailInboundSettings>();
        var keyMatches = EmailWebhookKey.Check(httpContext.Request, settings, secretProtector);

        // Without a key the webhook is not set up on this tenant, and it answers as if it did not exist.
        if (keyMatches is null)
        {
            return TypedResults.NotFound();
        }

        if (keyMatches == false)
        {
            logger.LogWarning("An inbound email webhook call for provider {Provider} carried a wrong or missing key and was refused.", provider.SanitizeLogValue());

            return TypedResults.Unauthorized();
        }

        var parser = parsers.FirstOrDefault(candidate => string.Equals(candidate.Name, provider, StringComparison.OrdinalIgnoreCase));

        if (parser is null)
        {
            return TypedResults.NotFound();
        }

        // An email with its attachments can be large; the limit is set for this call alone, before the body is read.
        var sizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = EmailChannelConstants.MaxInboundEmailBytes;
        }

        InboundEmailWebhookResult parsed;

        try
        {
            parsed = await parser.ParseAsync(httpContext.Request, settings, httpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "An inbound email webhook call for provider {Provider} could not be read.", provider.SanitizeLogValue());

            return TypedResults.BadRequest();
        }

        if (parsed.IsUnauthorized)
        {
            logger.LogWarning("An inbound email webhook call for provider {Provider} was refused: {Reason}", provider.SanitizeLogValue(), parsed.Reason.SanitizeLogValue());

            return TypedResults.Unauthorized();
        }

        if (parsed.IsInvalid)
        {
            logger.LogWarning("An inbound email webhook call for provider {Provider} was not valid: {Reason}", provider.SanitizeLogValue(), parsed.Reason.SanitizeLogValue());

            return TypedResults.BadRequest();
        }

        var accepted = new List<string>();

        foreach (var email in parsed.Emails)
        {
            var result = await receiver.ReceiveAsync(email, parser.Name, dispatch: false, httpContext.RequestAborted);

            switch (result.Status)
            {
                case EmailInboundStatus.Busy:
                    // The same email is being received by another call right now; asking the provider to retry is safe,
                    // because the inbox recognises the email when it comes back.
                    return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);

                case EmailInboundStatus.Accepted when !string.IsNullOrEmpty(result.InboxMessageId):
                    accepted.Add(result.InboxMessageId);
                    break;
            }
        }

        // Processed after the call is answered, so the provider never waits on the workspace or the AI.
        await EmailInboxBackgroundDispatch.DispatchAsync(shellHost, shellSettings, accepted);

        return TypedResults.Ok();
    }
}
