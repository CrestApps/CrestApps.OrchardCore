using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The source-generated log messages of the Contact Center core services. Their event ids are
/// <see cref="CrestAppsEventIds.ContactCenter"/> to <c>17599</c>; the Contact Center module uses <c>17600</c> to
/// <c>17899</c>.
/// </summary>
internal static partial class ContactCenterLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.ContactCenter + 100,
        Message = "Provider command '{ProviderCommandId}' ({CommandType}) to provider '{ProviderName}' settled as {Status} after {Attempts} attempt(s).")]
    public static partial void ProviderCommandSettled(
        ILogger logger,
        LogLevel level,
        string providerCommandId,
        ProviderCommandType commandType,
        string providerName,
        ProviderCommandStatus status,
        int attempts);
}
