using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
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
    private const long _maximumRequestBodySizeBytes = 1024 * 1024;

    // A concurrency conflict while storing an inbound text is retried in a fresh scope, because Event Grid has
    // already been answered and will not redeliver it. The delays sit between attempts 1-2 and 2-3.
    private const int _maximumInboundSmsAttempts = 3;

    private static readonly TimeSpan[] _inboundSmsRetryDelays =
    [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
    ];

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

        // A chunked request declares no length, so the ceiling has to be enforced against what actually arrives
        // rather than against what the caller says it will send.
        var read = await RequestBodyReader.ReadAsync(context.Request, _maximumRequestBodySizeBytes, context.RequestAborted);

        if (read.IsTooLarge)
        {
            logger.LogWarning("Event Grid payload exceeded the maximum supported size of {MaxBytes} bytes.", _maximumRequestBodySizeBytes);

            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
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
            return TypedResults.BadRequest();
        }

        foreach (var e in events)
        {
            // Event Grid sends this handshake before it delivers anything to a new webhook subscription, and only
            // activates the subscription once the validation code is echoed back in this exact JSON shape.
            if (string.Equals(e.EventType, EventGridEventTypes.SubscriptionValidation, StringComparison.OrdinalIgnoreCase))
            {
                var validation = e.Data?.ToObjectFromJson<SubscriptionValidationEventData>();

                if (string.IsNullOrEmpty(validation?.ValidationCode))
                {
                    logger.LogWarning("Rejected an Event Grid subscription validation event {EventId} that carried no validation code.", e.Id.SanitizeLogValue());

                    return TypedResults.BadRequest();
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Event Grid subscription validation received for event {EventId}; answering with the validation code.", e.Id.SanitizeLogValue());
                }

                return TypedResults.Json(new
                {
                    validationResponse = validation.ValidationCode,
                });
            }

            // The subject is not logged: for Azure Communication Services events it is the customer's phone number.
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Event Grid event received. EventType: {EventType}, EventId: {EventId}.", e.EventType.SanitizeLogValue(), e.Id.SanitizeLogValue());
            }

            var dataJson = e.Data?.ToString();

            var mapping = EventGridEventMapper.Map(e.EventType, dataJson);

            switch (mapping.Kind)
            {
                case EventGridEventMappingKind.SmsReceived:
                    await DispatchInboundSmsAsync(e, mapping, clock, shellHost, shellSettings, logger);

                    continue;

                case EventGridEventMappingKind.SmsDeliveryReport:
                    // Delivery receipts reach the Messaging workspace through a direct service call from a provider's
                    // own webhook, not through an Omnichannel event, and this module does not reference that service.
                    // The report is therefore stored and passed to the handlers unchanged, exactly as before.
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation(
                            "Event Grid event {EventId} is an SMS delivery report for message {ProviderMessageId} with status '{DeliveryStatus}' ('{DeliveryStatusDetails}'). Delivery reports are stored but not routed; the sent message's delivery status is not updated.",
                            e.Id.SanitizeLogValue(),
                            mapping.ProviderMessageId.SanitizeLogValue(),
                            mapping.DeliveryStatus.SanitizeLogValue(),
                            mapping.DeliveryStatusDetails.SanitizeLogValue());
                    }

                    break;

                case EventGridEventMappingKind.Malformed:
                    logger.LogWarning(
                        "Event Grid event {EventId} of type {EventType} could not be mapped because {Reason}. It is stored with its raw event type and not routed to a channel.",
                        e.Id.SanitizeLogValue(),
                        e.EventType.SanitizeLogValue(),
                        mapping.Reason);

                    break;

                default:
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug(
                            "Event Grid event {EventId} of type {EventType} has no Omnichannel mapping. It is stored with its raw event type and passed to the handlers unchanged.",
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
        }

        return TypedResults.Ok();
    }

    /// <summary>
    /// Raises an inbound text delivered through Event Grid as the platform's own SMS received event, in the same
    /// shape the Twilio and Telnyx webhooks produce, so SMS automation and the Messaging workspace both act on it.
    /// </summary>
    private static async Task DispatchInboundSmsAsync(
        EventGridEvent e,
        EventGridEventMapping mapping,
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

        // A retry must not reuse objects a failed attempt's handlers may have changed, so each attempt builds its
        // own message and event from the mapped values.
        OmnichannelEvent CreateEvent() => new()
        {
            Id = providerMessageId,
            EventType = mapping.EventName,
            Subject = "SMS received",
            Data = BinaryData.FromString(mapping.Content),
            Message = new OmnichannelMessage
            {
                CustomerAddress = mapping.From,
                ServiceAddress = mapping.To,
                Content = mapping.Content,
                Channel = mapping.Channel,
                CreatedUtc = createdUtc,
                IsInbound = true,
                ProviderMessageId = providerMessageId,
            },
        };

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Mapped Event Grid event {EventId} of type {EventType} to channel {Channel} and event {OmnichannelEvent}. ProviderMessageId: {ProviderMessageId}, Length: {Length}.",
                e.Id.SanitizeLogValue(),
                e.EventType.SanitizeLogValue(),
                mapping.Channel,
                mapping.EventName,
                providerMessageId.SanitizeLogValue(),
                mapping.Content.Length);
        }

        // An automated reply waits a humanized settle pause, calls the AI model and waits a "typing" pause, which
        // together can outlast the 30 seconds Event Grid waits for a response. Event Grid would then treat the
        // delivery as failed and send it again, racing the original for the same conversation. So, as the Twilio
        // webhook does, acknowledge now and process the text in a fresh shell scope off the request thread.
        _ = ProcessInboundSmsAsync(shellHost, shellSettings, CreateEvent, mapping.Channel, providerMessageId, logger);
    }

    private static async Task ProcessInboundSmsAsync(
        IShellHost shellHost,
        ShellSettings shellSettings,
        Func<OmnichannelEvent> createEvent,
        string channel,
        string providerMessageId,
        ILogger logger)
    {
        // Event Grid delivers at least once, so the same text can arrive again. Claim its id once, before it is
        // stored or any handler runs, so a redelivery is neither recorded twice nor answered again. The claim is
        // made on the first attempt only; a retry below is this same delivery and must not see itself as a
        // duplicate. The gate ships with Omnichannel management; without it, the handlers' own idempotency applies.
        var claimed = false;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var isDuplicate = false;

                // Each attempt runs in its own shell scope, so a retry starts from a clean session rather than one
                // whose commit already failed.
                var backgroundScope = await shellHost.GetScopeAsync(shellSettings);

                await backgroundScope.UsingAsync(async scope =>
                {
                    var scopedSession = scope.ServiceProvider.GetRequiredService<YesSqlSession>();
                    var scopedHandlers = scope.ServiceProvider.GetServices<IOmnichannelEventHandler>();
                    var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Startup>>();

                    using var logScope = scopedLogger.BeginScope(new Dictionary<string, object>
                    {
                        ["ProviderMessageId"] = providerMessageId.SanitizeLogValue(),
                        ["Channel"] = channel,
                    });

                    if (!claimed)
                    {
                        var conversationGate = scope.ServiceProvider.GetService<IAutomatedConversationGate>();

                        if (conversationGate is not null && !conversationGate.TryClaimInboundMessage(providerMessageId))
                        {
                            isDuplicate = true;

                            return;
                        }

                        claimed = true;
                    }

                    var omnichannelEvent = createEvent();

                    await scopedSession.SaveAsync(omnichannelEvent.Message, collection: OmnichannelConstants.CollectionName);

                    await scopedHandlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), omnichannelEvent, scopedLogger);
                });

                if (isDuplicate && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Ignoring a duplicate Event Grid SMS delivery for ProviderMessageId {ProviderMessageId}.", providerMessageId.SanitizeLogValue());
                }

                return;
            }
            catch (ConcurrencyException) when (attempt < _maximumInboundSmsAttempts)
            {
                // Another writer changed a document this attempt also wrote, and the session was not committed.
                // Event Grid has already been answered, so this is the only place the text can be retried.
                logger.LogWarning(
                    "Retrying inbound Event Grid SMS {ProviderMessageId} after a concurrency conflict (attempt {Attempt} of {MaxAttempts}).",
                    providerMessageId.SanitizeLogValue(),
                    attempt,
                    _maximumInboundSmsAttempts);

                await Task.Delay(_inboundSmsRetryDelays[attempt - 1]);
            }
            catch (Exception ex)
            {
                // Any other failure, or a concurrency conflict on the last attempt. Event Grid has already been
                // answered, so nothing upstream is left to retry this work, and an exception escaping here would be
                // unobserved. Record it so the lost text is visible in the logs.
                logger.LogError(ex, "Failed to process the inbound Event Grid SMS {ProviderMessageId} in the background.", providerMessageId.SanitizeLogValue());

                return;
            }
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
