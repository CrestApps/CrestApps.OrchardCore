using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
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
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OrchardCore.Modules;

internal static class AzureEventGridEndpoint
{
    private const long _maximumRequestBodySizeBytes = 1024 * 1024;

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
        var eventGridOptions = options.Value;

        // Check SAS key
        if (!string.IsNullOrEmpty(eventGridOptions.EventGridSasKey) &&
            context.Request.Headers.TryGetValue("aeg-sas-key", out var headerKey) &&
            FixedTimeEquals(headerKey.ToString(), eventGridOptions.EventGridSasKey))
        {
            isAuthorized = true;
        }

        // Check AAD token if SAS key failed
        if (!isAuthorized &&
            context.Request.Headers.TryGetValue("Authorization", out var authHeader) &&
            TryGetBearerToken(authHeader.ToString(), out var token))
        {
            try
            {
                if (!CanValidateAadToken(eventGridOptions))
                {
                    logger.LogWarning("AAD token validation is configured incompletely. Event Grid requires AADIssuer, AADAudience, and AADMetadataAddress.");
                }
                else
                {
                    isAuthorized = await ValidateAadTokenAsync(token, eventGridOptions, context.RequestAborted);
                }
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

        if (context.Request.ContentLength is > _maximumRequestBodySizeBytes)
        {
            logger.LogWarning("Event Grid payload exceeded the maximum supported size of {MaxBytes} bytes.", _maximumRequestBodySizeBytes);

            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
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

    private static bool CanValidateAadToken(EventGridOptions options) =>
        !string.IsNullOrWhiteSpace(options.AADIssuer) &&
        !string.IsNullOrWhiteSpace(options.AADAudience) &&
        !string.IsNullOrWhiteSpace(options.AADMetadataAddress);

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static bool TryGetBearerToken(string authorizationHeader, out string token)
    {
        token = null;

        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = authorizationHeader["Bearer ".Length..].Trim();

        return !string.IsNullOrEmpty(token);
    }

    private static async Task<bool> ValidateAadTokenAsync(
        string token,
        EventGridOptions options,
        CancellationToken cancellationToken)
    {
        var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            options.AADMetadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever
            {
                RequireHttps = options.AADMetadataAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
            });
        var openIdConfiguration = await configurationManager.GetConfigurationAsync(cancellationToken);
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.AADIssuer,
            ValidateAudience = true,
            ValidAudience = options.AADAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = openIdConfiguration.SigningKeys,
        };
        var handler = new JwtSecurityTokenHandler();
        var validationResult = await handler.ValidateTokenAsync(token, validationParameters);

        return validationResult.IsValid;
    }
}
