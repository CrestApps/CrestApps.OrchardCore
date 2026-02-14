using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.EventGrid;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrchardCore.Modules;

internal static class AzureEventGridEndpoint
{
    public static IEndpointRouteBuilder AddAzureEventGridEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("Omnichannel/webhook/AzureEventGrid", HandleAsync)
            .DisableAntiforgery()
            .AllowAnonymous();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IEnumerable<IOmnichannelEventHandler> handlers,
        YesSql.ISession session,
        IClock clock,
        IOptions<EventGridOptions> options,
        ILogger<Startup> logger)
    {
        var isAuthorized = false;

        // Check SAS key
        if (context.Request.Headers.TryGetValue("aeg-sas-key", out var headerKey) &&
            headerKey == options.Value.EventGridSasKey)
        {
            isAuthorized = true;
        }

        // Check AAD token if SAS key failed
        if (!isAuthorized && context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            // Expect "Bearer <token>"
            var token = authHeader.ToString()?.Replace("Bearer ", "");

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Value.AADIssuer,       // e.g., "https://sts.windows.net/{tenantId}/"
                    ValidateAudience = true,
                    ValidAudience = options.Value.AADAudience,   // e.g., your API App ID URI
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    // You may also provide IssuerSigningKeys if using JWKS endpoint
                };

                var handler = new JwtSecurityTokenHandler();

                var claimsPrincipal = await handler.ValidateTokenAsync(token, validationParameters);

                isAuthorized = claimsPrincipal != null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AAD token validation failed.");
            }
        }

        if (!isAuthorized)
        {
            logger.LogWarning("Unauthorized Event Grid request.");

            return TypedResults.Unauthorized();
        }

        // Read request body
        string body;
        using (var reader = new StreamReader(context.Request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        EventGridEvent[] events;
        try
        {
            events = EventGridEvent.ParseMany(BinaryData.FromString(body));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse Event Grid payload.");
            return TypedResults.BadRequest();
        }

        foreach (var e in events)
        {
            // Handle subscription validation
            if (e.EventType == "Microsoft.EventGrid.SubscriptionValidationEvent")
            {
                var data = e.Data.ToObjectFromJson<SubscriptionValidationEventData>();
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Subscription validation received. Code: {Code}", data.ValidationCode);
                }

                return TypedResults.Json(new
                {
                    validationResponse = data.ValidationCode,
                });
            }

            // Handle normal events
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Event received: {EventType}, Subject: {Subject}, Id: {Id}", e.EventType, e.Subject, e.Id);
            }


            var omnichannelMessage = new OmnichannelMessage
            {
                Channel = "Unknown",
                CreatedUtc = clock.UtcNow,
                IsInbound = true,
            };

            var dataJson = e.Data.ToString();

            try
            {
                using var doc = JsonDocument.Parse(dataJson);
                var root = doc.RootElement;

                var properties = root.EnumerateObject()
                     .ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

                // Attempt to extract common fields
                omnichannelMessage.CustomerAddress = GetStringProperty(properties, "from", "sender", "customer");

                omnichannelMessage.ServiceAddress = GetStringProperty(properties, "to", "recipient", "service");

                omnichannelMessage.Content = GetStringProperty(properties, "content", "message", "body", "text") ?? dataJson;

                omnichannelMessage.Channel = GetStringProperty(properties, "channel", "transport", "protocol") ?? "Unknown";

                if (properties.TryGetValue("timestamp", out var ts) && ts.TryGetDateTime(out var dt))
                {
                    omnichannelMessage.CreatedUtc = dt;
                }
            }
            catch
            {
                // fallback: store raw JSON in content
                omnichannelMessage.Content = dataJson;
            }

            await session.SaveAsync(omnichannelMessage, collection: OmnichannelConstants.CollectionName);

            var omnichannelEvent = new OmnichannelEvent()
            {
                Id = e.Id,
                EventType = e.EventType,
                Subject = e.Subject,
                Data = e.Data,
                Message = omnichannelMessage,
            };

            await handlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), omnichannelEvent, logger);
        }

        return TypedResults.Ok();
    }

    private static string GetStringProperty(Dictionary<string, JsonElement> data, params string[] names)
    {
        var validNames = names.Where(name => data.TryGetValue(name, out var element) && element.ValueKind == JsonValueKind.String);
        
        foreach (var name in validNames)
        {
            return data[name].GetString();
        }

        return null;
    }
}
