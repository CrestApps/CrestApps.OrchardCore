using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Taxation.Core.Telemetry;

/// <summary>
/// The meter and activity source of taxation, <c>CrestApps.OrchardCore.Taxation</c>: how tax is determined, by an
/// external tax service or by the store's own rules, and how often a service that could not answer left it to the
/// store's rules. Addresses, items and amounts go nowhere on a metric.
/// </summary>
public static class TaxationTelemetry
{
    /// <summary>
    /// The name of the taxation meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Taxation";

    /// <summary>
    /// The <c>provider</c> of tax worked out by the store's own rules.
    /// </summary>
    public const string StoreRulesProvider = "store_rules";

    /// <summary>
    /// The meter of taxation.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Taxation");

    /// <summary>
    /// The activity source of taxation.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Taxation");

    /// <summary>
    /// The duration of tax determinations, tagged with the <c>provider</c> (the determination provider's type name,
    /// or <see cref="StoreRulesProvider"/>) and the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> DeterminationDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.taxation.determination.duration",
        "The duration of working out the tax of a sale, by an external tax service or the store's own rules.");

    /// <summary>
    /// The number of times an external tax service could not answer, so the store's own rules worked out the tax,
    /// tagged with the <c>provider</c> that failed and its <c>outcome</c>.
    /// </summary>
    public static Counter<long> Fallbacks { get; } = Meter.CreateCounter<long>(
        "crestapps.taxation.determination.fallbacks",
        "{determination}",
        "The number of times an external tax service could not answer and the store's own rules worked out the tax.");

    /// <summary>
    /// Starts measuring a tax determination.
    /// </summary>
    /// <param name="provider">The determination provider's type name, or <see cref="StoreRulesProvider"/>.</param>
    /// <param name="external">Whether the provider calls a service outside the site.</param>
    /// <returns>The scope, which records the <see cref="DeterminationDuration"/> when disposed.</returns>
    public static TelemetryScope StartDetermination(string provider, bool external)
    {
        return TelemetryScope.Start(
            ActivitySource,
            "crestapps.taxation.determination",
            DeterminationDuration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            },
            external ? ActivityKind.Client : ActivityKind.Internal);
    }

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
