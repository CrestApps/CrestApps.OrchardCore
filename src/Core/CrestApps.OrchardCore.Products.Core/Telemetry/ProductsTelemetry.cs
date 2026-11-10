using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Products.Core.Telemetry;

/// <summary>
/// The meter and activity source of products, <c>CrestApps.OrchardCore.Products</c>: the full rebuilds of the product
/// search records. Product ids and names go nowhere on a metric.
/// </summary>
public static class ProductsTelemetry
{
    /// <summary>
    /// The name of the products meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Products";

    /// <summary>
    /// The meter of products.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Products");

    /// <summary>
    /// The activity source of products.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Products");

    /// <summary>
    /// The duration of full rebuilds of the product search records, tagged with the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> SearchRebuildDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.products.search.rebuild.duration",
        "The duration of a full rebuild of the product search records.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of products a full rebuild of the product search records indexed.
    /// </summary>
    public static Histogram<long> SearchRebuildDocuments { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.products.search.rebuild.documents",
        "{document}",
        "The number of products a full rebuild of the product search records indexed.");

    /// <summary>
    /// The Unix time, in seconds, the product search records were last rebuilt in full.
    /// </summary>
    public static Gauge<long> SearchLastRebuild { get; } = Meter.CreateGauge<long>(
        "crestapps.products.search.last_rebuild",
        "s",
        "The Unix time the product search records were last rebuilt in full.");

    /// <summary>
    /// Starts measuring a full rebuild of the product search records.
    /// </summary>
    /// <returns>The scope, which records the <see cref="SearchRebuildDuration"/> when disposed.</returns>
    public static TelemetryScope StartSearchRebuild()
    {
        return TelemetryScope.Start(ActivitySource, "crestapps.products.search.rebuild", SearchRebuildDuration);
    }

    /// <summary>
    /// Records a full rebuild of the product search records that finished.
    /// </summary>
    /// <param name="documents">The number of products it indexed.</param>
    /// <param name="timeProvider">The time provider that says when it finished.</param>
    public static void RecordSearchRebuilt(long documents, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var tags = new TagList();

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        if (SearchRebuildDocuments.Enabled)
        {
            SearchRebuildDocuments.Record(documents, tags);
        }

        if (SearchLastRebuild.Enabled)
        {
            SearchLastRebuild.Record(timeProvider.GetUtcNow().ToUnixTimeSeconds(), tags);
        }
    }
}
