using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Telemetry;

/// <summary>
/// The source-generated log messages of the Contact Center module. Their event ids are <c>17600</c> to <c>17899</c>;
/// the Contact Center core services use <see cref="CrestAppsEventIds.ContactCenter"/> to <c>17599</c>. Phone numbers
/// and SIP addresses are passed through the address redactor before they get here.
/// </summary>
internal static partial class ContactCenterModuleLog
{
    private const int First = CrestAppsEventIds.ContactCenter + 600;

    [LoggerMessage(
        EventId = First,
        Level = LogLevel.Error,
        Message = "An error occurred while scheduling a callback to '{Destination}'.")]
    public static partial void CallbackSchedulingFailed(ILogger logger, Exception exception, string destination);

    [LoggerMessage(
        EventId = First + 1,
        Level = LogLevel.Error,
        Message = "An error occurred while redelivering Contact Center outbox messages.")]
    public static partial void OutboxRedeliveryFailed(ILogger logger, Exception exception);
}
