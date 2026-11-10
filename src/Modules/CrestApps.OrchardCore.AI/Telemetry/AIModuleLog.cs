using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Telemetry;

/// <summary>
/// The log messages of the AI module, with event ids 11100 to 11199 of <see cref="CrestAppsEventIds.AI"/>.
/// </summary>
internal static partial class AIModuleLog
{
    /// <summary>
    /// A signed webhook delivery failed and is tried again.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.AI + 100, Level = LogLevel.Warning, Message = "The signed webhook '{DeliveryId}' failed with status code {StatusCode} and is tried again (attempt {Attempt}/{MaxAttempts}).")]
    public static partial void SignedWebhookRetrying(ILogger logger, string deliveryId, int? statusCode, int attempt, int maxAttempts);
}
