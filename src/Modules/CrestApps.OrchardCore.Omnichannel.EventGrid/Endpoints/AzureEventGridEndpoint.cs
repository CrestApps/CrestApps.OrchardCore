using System.IdentityModel.Tokens.Jwt;
using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.EventGrid;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Models;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Services;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core.Signatures;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using YesSql;
using YesSqlSession = YesSql.ISession;

internal static class AzureEventGridEndpoint
{
    /// <summary>
    /// The provider name inbound texts are keyed under in the durable provider webhook inbox.
    /// </summary>
    internal const string ProviderName = "AzureEventGrid";

    private const long _maximumRequestBodySizeBytes = 1024 * 1024;

    private static readonly SharedKeyWebhookSignatureValidator _sharedKeyValidator = new();

    /// <summary>
    /// Adds the azure event grid endpoint.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static IEndpointRouteBuilder AddAzureEventGridEndpoint(this IEndpointRouteBuilder builder)
    {
        // Provider webhooks follow the api/{provider}/webhook/{action} convention. This endpoint is the Azure
        // Event Grid delivery firehose (not SMS-specific), so the action names the mechanism it receives.
        _ = builder.MapPost("api/azure/webhook/eventgrid", HandleAsync)
            .DisableAntiforgery()
            .AllowAnonymous();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IEnumerable<IOmnichannelEventHandler> handlers,
        YesSqlSession session,
        IClock clock,
        IOptions<EventGridOptions> options,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<Startup> logger)
    {
        var (result, requestResult) = await HandleCoreAsync(context, handlers, session, clock, options.Value, shellHost, shellSettings, logger);

        // What became of the delivery goes on the server span ASP.NET Core records around it.
        Activity.Current?.SetTag("crestapps.eventgrid.result", requestResult);

        return result;
    }

    private static async Task<(IResult Result, string RequestResult)> HandleCoreAsync(
        HttpContext context,
        IEnumerable<IOmnichannelEventHandler> handlers,
        YesSqlSession session,
        IClock clock,
        EventGridOptions eventGridOptions,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger logger)
    {
        var isAuthorized = false;

        // Check SAS key
        if (!string.IsNullOrEmpty(eventGridOptions.EventGridSasKey) &&
            context.Request.Headers.TryGetValue("aeg-sas-key", out var headerKey) &&
            _sharedKeyValidator.IsValid(new WebhookSignatureValidationContext
            {
                Secret = eventGridOptions.EventGridSasKey,
                Signature = headerKey.ToString(),
            }))
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

            return (TypedResults.Unauthorized(), EventGridTelemetry.RequestResults.Unauthorized);
        }

        // A chunked request declares no length, so the ceiling has to be enforced against what actually arrives
        // rather than against what the caller says it will send.
        var read = await RequestBodyReader.ReadAsync(context.Request, _maximumRequestBodySizeBytes, context.RequestAborted);

        if (read.IsTooLarge)
        {
            logger.LogWarning("Event Grid payload exceeded the maximum supported size of {MaxBytes} bytes.", _maximumRequestBodySizeBytes);

            return (TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge), EventGridTelemetry.RequestResults.TooLarge);
        }

        var body = read.Body;

        EventGridEvent[] events;
        try
        {
            events = EventGridEvent.ParseMany(BinaryData.FromString(body));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse Event Grid payload.");
            return (TypedResults.BadRequest(), EventGridTelemetry.RequestResults.ParseError);
        }

        foreach (var e in events)
        {
            // The subject is never in the scope: for Azure Communication Services events it is the customer's phone number.
            using var eventScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["EventGridEventId"] = e.Id.SanitizeLogValue(),
                ["EventType"] = e.EventType.SanitizeLogValue(),
            });

            // Event Grid sends this handshake before it delivers anything to a new webhook subscription, and only
            // activates the subscription once the validation code is echoed back in this exact JSON shape.
            if (string.Equals(e.EventType, EventGridEventTypes.SubscriptionValidation, StringComparison.OrdinalIgnoreCase))
            {
                var validation = e.Data?.ToObjectFromJson<SubscriptionValidationEventData>();

                if (string.IsNullOrEmpty(validation?.ValidationCode))
                {
                    logger.SubscriptionValidationWithoutCode(e.Id.SanitizeLogValue());
                    EventGridTelemetry.RecordEvent(e.EventType, EventGridTelemetry.EventResults.Invalid);

                    return (TypedResults.BadRequest(), EventGridTelemetry.RequestResults.Invalid);
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.SubscriptionValidationReceived(e.Id.SanitizeLogValue());
                }

                EventGridTelemetry.RecordEvent(e.EventType, EventGridTelemetry.EventResults.Validated);

                return (TypedResults.Json(new
                {
                    validationResponse = validation.ValidationCode,
                }), EventGridTelemetry.RequestResults.Validated);
            }

            // The subject is not logged: for Azure Communication Services events it is the customer's phone number.
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.EventReceived(e.EventType.SanitizeLogValue(), e.Id.SanitizeLogValue());
            }

            var dataJson = e.Data?.ToString();

            var mapping = EventGridEventMapper.Map(e.EventType, dataJson);
            var eventResult = EventGridTelemetry.EventResults.Stored;

            switch (mapping.Kind)
            {
                case EventGridEventMappingKind.SmsReceived:
                    var accepted = await AcceptInboundSmsAsync(context, e, mapping, handlers, session, clock, shellHost, shellSettings, logger);

                    EventGridTelemetry.RecordEvent(e.EventType, accepted);

                    if (accepted is EventGridTelemetry.EventResults.Busy or EventGridTelemetry.EventResults.Failed)
                    {
                        // The text was not stored, so Event Grid has to deliver the batch again. The texts of this
                        // batch that were stored are recognised as duplicates then.
                        return (TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable), EventGridTelemetry.RequestResults.Retry);
                    }

                    continue;

                case EventGridEventMappingKind.SmsDeliveryReport:
                    // Delivery receipts reach the Messaging workspace through a direct service call from a provider's
                    // own webhook, not through an Omnichannel event, and this module does not reference that service.
                    // The report is therefore stored and passed to the handlers unchanged, exactly as before.
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.DeliveryReportStored(
                            e.Id.SanitizeLogValue(),
                            mapping.ProviderMessageId.SanitizeLogValue(),
                            mapping.DeliveryStatus.SanitizeLogValue(),
                            mapping.DeliveryStatusDetails.SanitizeLogValue());
                    }

                    break;

                case EventGridEventMappingKind.Malformed:
                    logger.EventMalformed(
                        e.Id.SanitizeLogValue(),
                        e.EventType.SanitizeLogValue(),
                        mapping.Reason);
                    eventResult = EventGridTelemetry.EventResults.Malformed;

                    break;

                default:
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.EventUnmapped(
                            e.Id.SanitizeLogValue(),
                            e.EventType.SanitizeLogValue());
                    }

                    break;
            }

            var omnichannelMessage = new OmnichannelMessage
            {
                Channel = "Unknown",
                CreatedUtc = clock.UtcNow,
                IsInbound = true,
            };

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

            EventGridTelemetry.RecordEvent(e.EventType, eventResult);
        }

        return (TypedResults.Ok(), EventGridTelemetry.RequestResults.Ok);
    }

    /// <summary>
    /// Accepts an inbound text delivered through Event Grid as the platform's own SMS received event, in the same
    /// shape the Twilio and Telnyx webhooks produce, so SMS automation and the Messaging workspace both act on it.
    /// </summary>
    /// <remarks>
    /// An automated reply waits a humanized settle pause, calls the AI model and waits a "typing" pause, which together
    /// can outlast the 30 seconds Event Grid waits for a response. So the text is committed to the durable provider
    /// webhook inbox, keyed on the provider's message id, before Event Grid is answered, and processing starts once the
    /// response is sent. A redelivery is absorbed by the inbox on any node, and processing that fails part-way is
    /// retried from storage instead of being lost with a discarded background task.
    /// </remarks>
    /// <returns>
    /// What became of the text, one of <see cref="EventGridTelemetry.EventResults"/>: <c>accepted</c>, <c>duplicate</c>
    /// or <c>stored</c> when the text is stored (or was already stored); <c>busy</c> or <c>failed</c> when it was not,
    /// so Event Grid has to deliver it again.
    /// </returns>
    internal static async Task<string> AcceptInboundSmsAsync(
        HttpContext context,
        EventGridEvent e,
        EventGridEventMapping mapping,
        IEnumerable<IOmnichannelEventHandler> handlers,
        YesSqlSession session,
        IClock clock,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger logger)
    {
        var now = clock.UtcNow;

        // The provider's receive time keeps a text that Event Grid redelivers later in its true place in the
        // thread; a timestamp ahead of this server's clock is ignored so a reply can never sort before the text.
        var createdUtc = mapping.ReceivedUtc is { } receivedUtc && receivedUtc <= now
            ? receivedUtc
            : now;

        // The provider's message id is the correlation and de-duplication key, as Twilio's MessageSid is. Fall back
        // to the Event Grid event id, which Event Grid also keeps stable across redeliveries of the same event.
        var providerMessageId = string.IsNullOrEmpty(mapping.ProviderMessageId)
            ? e.Id
            : mapping.ProviderMessageId;

        var message = new OmnichannelMessage
        {
            CustomerAddress = mapping.From,
            ServiceAddress = mapping.To,
            Content = mapping.Content,
            Channel = mapping.Channel,
            CreatedUtc = createdUtc,
            IsInbound = true,
            ProviderMessageId = providerMessageId,
        };

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.EventMapped(
                e.Id.SanitizeLogValue(),
                e.EventType.SanitizeLogValue(),
                mapping.Channel,
                mapping.EventName,
                providerMessageId.SanitizeLogValue(),
                mapping.Content.Length);
        }

        var inbox = context.RequestServices.GetService<IProviderWebhookInbox>();
        var hasInboxHandler = context.RequestServices.GetServices<IProviderWebhookInboxHandler>()
            .Any(handler => string.Equals(handler.TechnicalName, OmnichannelConstants.WebhookInboxHandlers.SmsReceived, StringComparison.Ordinal));

        if (inbox is null || !hasInboxHandler || string.IsNullOrEmpty(providerMessageId))
        {
            // No SMS feature is on to take the text (each SMS feature brings the inbox and its handler), so nothing
            // composes an automated reply. Store it and hand it to the event handlers inline, as any other event is.
            await session.SaveAsync(message, collection: OmnichannelConstants.CollectionName);

            await handlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), new OmnichannelEvent
            {
                Id = providerMessageId,
                EventType = mapping.EventName,
                Subject = "SMS received",
                Data = BinaryData.FromString(mapping.Content),
                Message = message,
            }, logger);

            return EventGridTelemetry.EventResults.Stored;
        }

        ProviderWebhookInboxAcceptanceResult acceptance;

        try
        {
            acceptance = await inbox.AcceptAsync(
                new ProviderWebhookInboxDelivery
                {
                    ProviderName = ProviderName,
                    DeliveryId = providerMessageId,
                    HandlerName = OmnichannelConstants.WebhookInboxHandlers.SmsReceived,
                    Payload = JsonSerializer.Serialize(message),
                },
                context.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing was stored, so nothing picks this up later: this is the one case where Event Grid must redeliver.
            logger.LogError(exception, "The inbound Event Grid SMS {ProviderMessageId} could not be stored in the provider webhook inbox.", providerMessageId.SanitizeLogValue());

            return EventGridTelemetry.EventResults.Failed;
        }

        if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Duplicate)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Ignoring a duplicate Event Grid SMS delivery for ProviderMessageId {ProviderMessageId}.", providerMessageId.SanitizeLogValue());
            }

            return EventGridTelemetry.EventResults.Duplicate;
        }

        if (acceptance.Status != ProviderWebhookInboxAcceptanceStatus.Accepted)
        {
            // Busy: the delivery was not stored.
            return EventGridTelemetry.EventResults.Busy;
        }

        var messageId = acceptance.MessageId;

        AfterResponseWork.Schedule(
            context,
            shellHost,
            shellSettings,
            scope => DispatchAsync(scope, messageId),
            logger);

        return EventGridTelemetry.EventResults.Accepted;
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
