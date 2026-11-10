using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Telemetry;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What a dispatch of a provider command reports about itself beyond its span and metrics: the settlement log line.
/// </summary>
public sealed partial class ProviderCommandProcessor
{
    /// <summary>
    /// Logs a command that a dispatch settled, with an event id an alert can be keyed on.
    /// </summary>
    private void LogSettled(ProviderCommand command, ProviderCommandStatus startStatus, ProviderCommand result)
    {
        if (result is null || result.Status == startStatus || !result.IsTerminal)
        {
            return;
        }

        // A failure or a compensation is what an operator looks for; a confirmation is routine.
        var level = result.Status == ProviderCommandStatus.Confirmed ? LogLevel.Debug : LogLevel.Information;

        ContactCenterLog.ProviderCommandSettled(_logger, level, command.CommandId, command.CommandType, command.ProviderName, result.Status, result.AttemptCount);
    }
}
