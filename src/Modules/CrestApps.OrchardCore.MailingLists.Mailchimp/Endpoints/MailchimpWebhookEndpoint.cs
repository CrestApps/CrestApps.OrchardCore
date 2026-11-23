using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core.Signatures;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Endpoints;

/// <summary>
/// Receives a mailing list's Mailchimp webhooks at <c>mailing-lists/mailchimp/webhooks/{listId}/{secret}</c>. A webhook
/// is checked first (the size, the secret in its address and, when the list has a signing secret, its signature and
/// age), then stored in the provider webhook inbox before anything acts on it, and acted on after the answer is sent.
/// Mailchimp sends no event id, so the delivery id is the hash of the list id and the body: a resent webhook is stored
/// once, and two lists on the same audience each keep their own copy.
/// </summary>
internal static class MailchimpWebhookEndpoint
{
    /// <summary>
    /// The largest body accepted. Mailchimp's webhooks are a few kilobytes at most.
    /// </summary>
    internal const int MaxBodyLength = 64 * 1024;

    public static IEndpointRouteBuilder AddMailchimpWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(MailchimpConstants.WebhookRoutePattern, HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithMetadata(
                new RouteNameMetadata(MailchimpConstants.WebhookRouteName),
                new EndpointNameMetadata(MailchimpConstants.WebhookRouteName));

        // Whether Mailchimp checks the address with a GET when the webhook is created is not documented. The answer
        // reveals nothing and changes nothing, so it does not depend on the list or the secret.
        builder.MapGet(MailchimpConstants.WebhookRoutePattern, () => TypedResults.Ok())
            .AllowAnonymous();

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string listId,
        string secret,
        HttpContext context,
        IMailingListManager listManager,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<MailchimpWebhookInboxHandler> logger)
    {
        var receipt = new WebhookReceipt();

        try
        {
            return await HandleCoreAsync(listId, secret, context, listManager, dataProtectionProvider, clock, shellHost, shellSettings, logger, receipt);
        }
        finally
        {
            MailchimpTelemetry.RecordWebhook(receipt.Result ?? MailchimpTelemetry.WebhookError, receipt.Type);
        }
    }

    private static async Task<IResult> HandleCoreAsync(
        string listId,
        string secret,
        HttpContext context,
        IMailingListManager listManager,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger logger,
        WebhookReceipt receipt)
    {
        var inbox = context.RequestServices.GetService<IProviderWebhookInbox>();

        if (inbox is null)
        {
            logger.LogError("The provider webhook inbox is not registered, so a Mailchimp webhook could not be stored.");

            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (context.Request.ContentLength > MaxBodyLength)
        {
            receipt.Result = MailchimpTelemetry.WebhookRejectedBody;

            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var limiter = context.RequestServices.GetService<IProviderWebhookIngressLimiter>();
        ProviderWebhookIngressLease concurrencyLease = null;
        ProviderWebhookIngressLease rateLease = null;

        try
        {
            if (limiter is not null)
            {
                concurrencyLease = await limiter.AcquireConcurrencyAsync(context.RequestAborted);

                if (!concurrencyLease.IsAcquired)
                {
                    SetRetryAfter(context, concurrencyLease.RetryAfter);
                    receipt.Result = MailchimpTelemetry.WebhookBusy;

                    return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
                }
            }

            var list = string.IsNullOrEmpty(listId) ? null : await listManager.FindByIdAsync(listId, context.RequestAborted);

            if (list is null || !list.IsMailchimpList())
            {
                receipt.Result = MailchimpTelemetry.WebhookRejectedUrl;

                return TypedResults.NotFound();
            }

            var settings = list.GetMailchimpSettings();
            var urlSecret = Unprotect(dataProtectionProvider, settings.WebhookUrlSecret);

            if (urlSecret is null || !WebhookSignatures.FixedTimeEquals(urlSecret, secret))
            {
                receipt.Result = MailchimpTelemetry.WebhookRejectedUrl;

                return TypedResults.NotFound();
            }

            var read = await RequestBodyReader.ReadAsync(context.Request, MaxBodyLength, context.RequestAborted);

            if (read.IsTooLarge)
            {
                receipt.Result = MailchimpTelemetry.WebhookRejectedBody;

                return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var body = read.Body ?? string.Empty;

            if (!string.IsNullOrEmpty(settings.WebhookSigningSecret))
            {
                var signingSecret = Unprotect(dataProtectionProvider, settings.WebhookSigningSecret);

                if (signingSecret is null)
                {
                    logger.LogError("The signing secret of a mailing list's Mailchimp webhook can't be read with this site's data protection keys. Register the webhook again.");
                    receipt.Result = MailchimpTelemetry.WebhookRejectedSignature;

                    return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                var check = MailchimpWebhookSignature.Verify(context.Request.Headers[MailchimpConstants.SignatureHeaderName].ToString(), body, signingSecret, clock.UtcNow);

                if (check != MailchimpSignatureResult.Valid)
                {
                    logger.LogWarning("Rejected a Mailchimp webhook because its signature is {SignatureResult}.", check);
                    receipt.Result = MailchimpTelemetry.WebhookRejectedSignature;

                    return TypedResults.Unauthorized();
                }
            }

            var webhook = MailchimpWebhookParser.Parse(body);

            if (webhook is null)
            {
                receipt.Result = MailchimpTelemetry.WebhookRejectedBody;

                return TypedResults.BadRequest();
            }

            receipt.Type = webhook.Type;

            if (!MailchimpWebhookParser.IsHandled(webhook.Type))
            {
                receipt.Result = MailchimpTelemetry.WebhookIgnored;

                return TypedResults.Ok();
            }

            if (!string.IsNullOrEmpty(webhook.AudienceId) && !string.Equals(webhook.AudienceId, settings.AudienceId, StringComparison.Ordinal))
            {
                receipt.Result = MailchimpTelemetry.WebhookRejectedAudience;

                return TypedResults.BadRequest();
            }

            if (limiter is not null)
            {
                rateLease = await limiter.AcquireRateAsync(MailchimpConstants.WebhookProviderName, context.RequestAborted);

                if (!rateLease.IsAcquired)
                {
                    SetRetryAfter(context, rateLease.RetryAfter);
                    receipt.Result = MailchimpTelemetry.WebhookBusy;

                    return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
                }
            }

            ProviderWebhookInboxAcceptanceResult acceptance;

            try
            {
                acceptance = await inbox.AcceptAsync(
                    new ProviderWebhookInboxDelivery
                    {
                        ProviderName = MailchimpConstants.WebhookProviderName,
                        DeliveryId = GetDeliveryId(list.ItemId, body),
                        HandlerName = MailchimpConstants.WebhookHandlerName,
                        Payload = JsonSerializer.Serialize(
                            new MailchimpWebhookPayload
                            {
                                ListId = list.ItemId,
                                Body = body,
                                ReceivedUtc = clock.UtcNow,
                            },
                            JsonSerializerOptions.Web),
                    },
                    context.RequestAborted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Nothing was stored: Mailchimp sends the webhook again when it gets no 200.
                logger.LogError(exception, "A Mailchimp webhook could not be stored in the provider webhook inbox.");
                receipt.Result = MailchimpTelemetry.WebhookError;

                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Duplicate)
            {
                receipt.Result = MailchimpTelemetry.WebhookDuplicate;

                return TypedResults.Ok();
            }

            if (acceptance.Status != ProviderWebhookInboxAcceptanceStatus.Accepted)
            {
                receipt.Result = MailchimpTelemetry.WebhookBusy;

                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            receipt.Result = MailchimpTelemetry.WebhookAccepted;

            var messageId = acceptance.MessageId;

            AfterResponseWork.Schedule(context, shellHost, shellSettings, scope => DispatchAsync(scope, messageId), logger);

            return TypedResults.Ok();
        }
        finally
        {
            rateLease?.Dispose();
            concurrencyLease?.Dispose();
        }
    }

    /// <summary>
    /// Returns the delivery id of a webhook: the lowercase hex SHA-256 of the list id, a line break and the body.
    /// </summary>
    internal static string GetDeliveryId(string listId, string body)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{listId}\n{body}")));

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

    private static string Unprotect(IDataProtectionProvider dataProtectionProvider, string protectedValue)
    {
        try
        {
            var value = dataProtectionProvider.UnprotectSetting(MailchimpConstants.WebhookSecretPurpose, protectedValue);

            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static void SetRetryAfter(HttpContext context, TimeSpan? retryAfter)
    {
        if (retryAfter is { } delay && delay > TimeSpan.Zero)
        {
            context.Response.Headers.RetryAfter = Math.Ceiling(delay.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
    }

    private sealed class WebhookReceipt
    {
        public string Result { get; set; }

        public string Type { get; set; }
    }
}
