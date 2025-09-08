using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Endpoints;

internal static class CommunicationServiceEndpoint
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IEndpointRouteBuilder AddCommunicationServiceEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("Omnichannel/CommunicationService", HandleAsync)
            .DisableAntiforgery()
            .AllowAnonymous();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IEnumerable<IOmnichannelEventHandler> handlers,
        YesSql.ISession session,
        IClock clock,
        IOptions<CommunicationServiceOptions> options,
        ILogger<Startup> logger)
    {
        // Enable buffering so we can read the body multiple times
        context.Request.EnableBuffering();

        // Read request body
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // reset stream for later reading
        }

        // Validate HMAC signature (shared secret)
        if (!string.IsNullOrEmpty(options.Value.SharedSecret))
        {
            if (!context.Request.Headers.TryGetValue("X-MS-Signature", out var signatureHeader) ||
                !ValidateHmacSignature(body, signatureHeader, options.Value.SharedSecret))
            {
                logger.LogWarning("Unauthorized ACS request. Invalid signature.");

                return TypedResults.Unauthorized();
            }
        }

        // Webhook validation
        if (!string.IsNullOrEmpty(options.Value.WebhookValidationToken))
        {
            // ACS may send a validation request with a specific token
            if (body.Contains(options.Value.WebhookValidationToken))
            {
                logger.LogInformation("Webhook validation request received.");
                return TypedResults.Ok(new { status = "validated" });
            }
        }

        // Deserialize incoming ACS message
        CommunicationMessage message;
        try
        {
            message = JsonSerializer.Deserialize<CommunicationMessage>(body, _options)
                ?? throw new InvalidOperationException("Payload is empty.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse ACS message.");

            return TypedResults.BadRequest();
        }

        logger.LogInformation("Incoming ACS message: Channel={Channel}, From={From}, Content={Content}", message.Channel, message.From, message.Content);

        var omnichannelMessage = new OmnichannelMessage
        {
            Channel = message.Channel,
            CustomerAddress = message.From,
            ServiceAddress = message.To,
            Content = message.Content,
            CreatedUtc = message.Timestamp ?? clock.UtcNow,
            IsInbound = true,
        };

        await session.SaveAsync(omnichannelMessage, collection: OmnichannelConstants.CollectionName);

        var omnichannelEvent = new OmnichannelEvent
        {
            Id = message.Id ?? Guid.NewGuid().ToString(),
            EventType = message.Type ?? "ACSMessage",
            Subject = message.Channel ?? "ACS",
            Data = BinaryData.FromObjectAsJson(message),
            Message = omnichannelMessage,
        };

        await handlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), omnichannelEvent, logger);

        return TypedResults.Ok();
    }

    /// <summary>
    /// Validates HMAC SHA256 signature sent by ACS webhook
    /// </summary>
    private static bool ValidateHmacSignature(string body, string signatureHeader, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var expectedSignature = Convert.ToBase64String(hash);
        return expectedSignature == signatureHeader;
    }
}
