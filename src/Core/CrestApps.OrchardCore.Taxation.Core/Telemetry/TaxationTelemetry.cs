using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Taxation.Core.Telemetry;

/// <summary>
/// The meter of taxation, <c>CrestApps.OrchardCore.Taxation</c>: how often an external tax service that could not
/// answer left the tax to the store's own rules. Addresses, items and amounts go nowhere on a metric.
/// </summary>
public static class TaxationTelemetry
{
    /// <summary>
    /// The name of the taxation meter.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Taxation";

    /// <summary>
    /// The meter of taxation.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Taxation");

    /// <summary>
    /// The number of times an external tax service could not answer, so the store's own rules worked out the tax,
    /// tagged with the <c>provider</c> that failed and its <c>outcome</c>.
    /// </summary>
    public static Counter<long> Fallbacks { get; } = Meter.CreateCounter<long>(
        "crestapps.taxation.determination.fallbacks",
        "{determination}",
        "The number of times an external tax service could not answer and the store's own rules worked out the tax.");

    /// <summary>
    /// Records that an external tax service could not answer and the store's own rules worked out the tax.
    /// </summary>
    /// <param name="provider">The determination provider's type name.</param>
    /// <param name="outcome">How the provider failed, such as <c>error</c> or <c>timeout</c>.</param>
    public static void RecordFallback(string provider, string outcome)
    {
        if (!Fallbacks.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Fallbacks.Add(1, tags);
    }
}
