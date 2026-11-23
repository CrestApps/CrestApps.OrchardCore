using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Plaid.Services;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Plaid.Endpoints;

/// <summary>
/// Receives Plaid's webhooks. A webhook is checked first (its signature, age and body hash), then stored in the provider
/// webhook inbox before anything acts on it, and acted on after the answer is sent. Plaid sends no event id, so the
/// body's hash is the delivery id: a resent webhook is the same body and is stored once.
/// </summary>
internal static class PlaidWebhookEndpoint
{
    /// <summary>
    /// The largest body accepted. Plaid's webhooks are a few hundred bytes.
    /// </summary>
    internal const int MaxBodyLength = 64 * 1024;

    public static IEndpointRouteBuilder AddPlaidWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(PlaidConstants.WebhookPath, HandleAsync)
            .AllowAnonymous()
            .WithName(PlaidConstants.WebhookRouteName)
            .DisableAntiforgery();

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        HttpContext context,
        IPlaidCredentialsProvider credentialsProvider,
        PlaidWebhookVerifier verifier,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<PlaidWebhookVerifier> logger)
    {
        if (context.Request.ContentLength > MaxBodyLength)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = await ReadBodyAsync(context.Request, context.RequestAborted);

        if (body is null)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        if (await credentialsProvider.GetAsync(context.RequestAborted) is not { } credentials)
        {
            // Plaid is off here; nothing could act on the webhook.
            return TypedResults.NotFound();
        }

        if (!await verifier.VerifyAsync(credentials, context.Request.Headers["Plaid-Verification"].ToString(), body, context.RequestAborted))
        {
            return TypedResults.BadRequest();
        }

        var inbox = context.RequestServices.GetService<IProviderWebhookInbox>();

        if (inbox is null)
        {
            logger.LogError("The provider webhook inbox is not registered, so a Plaid webhook could not be stored.");

            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        ProviderWebhookInboxAcceptanceResult acceptance;

        try
        {
            acceptance = await inbox.AcceptAsync(
                new ProviderWebhookInboxDelivery
                {
                    ProviderName = PlaidConstants.ProviderName,
                    DeliveryId = Convert.ToHexStringLower(SHA256.HashData(body)),
                    HandlerName = PlaidConstants.WebhookHandlerName,
                    Payload = Encoding.UTF8.GetString(body),
                },
                context.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing was stored: Plaid sends the webhook again when it gets no 200.
            logger.LogError(exception, "A Plaid webhook could not be stored in the provider webhook inbox.");

            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Duplicate)
        {
            return TypedResults.Ok();
        }

        if (acceptance.Status != ProviderWebhookInboxAcceptanceStatus.Accepted)
        {
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var messageId = acceptance.MessageId;

        AfterResponseWork.Schedule(context, shellHost, shellSettings, scope => DispatchAsync(scope, messageId), logger);

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

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;

        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyLength)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
