using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Telemetry;

/// <summary>
/// The log messages of the tenant hierarchy, with event ids 20500 to 20599 of <see cref="CrestAppsEventIds.Startup"/>.
/// </summary>
internal static partial class TenantHierarchyLog
{
    /// <summary>
    /// An operation on a child tenant returned a failure.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Startup + 500, Level = LogLevel.Warning, Message = "The '{Operation}' operation on a child tenant did not succeed: {Error}")]
    public static partial void ChildOperationFailed(ILogger logger, string operation, string error);
}
