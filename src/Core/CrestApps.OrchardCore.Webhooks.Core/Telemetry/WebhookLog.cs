using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Webhooks.Core.Telemetry;

/// <summary>
/// The source-generated log messages of webhooks. Outbound deliveries use the event ids <c>17900</c> to <c>17949</c>
/// and the provider webhook inbox <c>17950</c> to <c>17999</c>, the end of <see cref="CrestAppsEventIds.ContactCenter"/>'s
/// range, where the inbox was first built.
/// </summary>
internal static partial class WebhookLog
{
    private const int Outbound = CrestAppsEventIds.ContactCenter + 900;

    private const int Inbox = CrestAppsEventIds.ContactCenter + 950;

    [LoggerMessage(
        EventId = Outbound + 1,
        Level = LogLevel.Warning,
        Message = "Webhook delivery '{DeliveryId}' of event '{EventName}' failed for good after {Attempt} attempt(s): {Reason}.")]
    public static partial void DeliveryFailed(ILogger logger, string deliveryId, string eventName, int attempt, string reason);

    [LoggerMessage(
        EventId = Inbox,
        Level = LogLevel.Error,
        Message = "Dead-lettered provider webhook inbox message '{MessageId}' from provider '{ProviderName}' for handler '{HandlerName}' after {Attempts} failed attempts: {Error}.")]
    public static partial void InboxMessageDeadLettered(
        ILogger logger,
        string messageId,
        string providerName,
        string handlerName,
        int attempts,
        string error);
}
