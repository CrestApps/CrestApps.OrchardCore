using CrestApps.OrchardCore.NLWeb.Telemetry;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Records every question the NLWeb ask pipeline answered on the <c>CrestApps.OrchardCore.NLWeb</c> meter: how long it
/// took, how it ended and how many results it returned. The question's text, site and ids go nowhere.
/// </summary>
internal sealed class NLWebTelemetryAskHandler : INLWebAskHandler
{
    /// <inheritdoc/>
    public Task AskedAsync(NLWebAskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        NLWebTelemetry.RecordAsk(context);

        return Task.CompletedTask;
    }
}
