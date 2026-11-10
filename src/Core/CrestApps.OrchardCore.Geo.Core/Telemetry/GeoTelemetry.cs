using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Geo.Core.Telemetry;

/// <summary>
/// The meter of geo services, <c>CrestApps.OrchardCore.Geo</c>: the travel-time cache, the fallbacks to the built-in
/// estimate and the background geocoding queue. Addresses, coordinates and record ids never go on a metric.
/// </summary>
public static class GeoTelemetry
{
    /// <summary>
    /// The name of the geo meter.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Geo";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> of a travel-time matrix.
    /// </summary>
    public const string OperationMatrix = "matrix";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> of a route between points.
    /// </summary>
    public const string OperationRoute = "route";

    /// <summary>
    /// A geocoding request found a place and wrote it back to its record.
    /// </summary>
    public const string GeocodeDone = "done";

    /// <summary>
    /// A geocoding request found no place that matches the address.
    /// </summary>
    public const string GeocodeNoMatch = "no_match";

    /// <summary>
    /// A geocoding request's record is gone.
    /// </summary>
    public const string GeocodeGone = "gone";

    /// <summary>
    /// Nothing writes a geocoding request's result back to its kind of record.
    /// </summary>
    public const string GeocodeNoTarget = "no_target";

    /// <summary>
    /// No geocoding provider is set up, so the request waits.
    /// </summary>
    public const string GeocodeNoProvider = "no_provider";

    /// <summary>
    /// The provider failed and the request is tried again later.
    /// </summary>
    public const string GeocodeRetry = "retry";

    /// <summary>
    /// The provider failed too many times and the request was given up.
    /// </summary>
    public const string GeocodeFailed = "failed";

    /// <summary>
    /// The meter of geo services.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Geo");

    /// <summary>
    /// The number of failed provider calls answered with the built-in straight-line estimate instead, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/> and the <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/>.
    /// </summary>
    public static Counter<long> Fallbacks { get; } = Meter.CreateCounter<long>(
        "crestapps.geo.provider.fallbacks",
        "{call}",
        "The number of failed travel-time and route calls answered with the built-in estimate.");

    /// <summary>
    /// The number of travel-time matrix lookups in the cache, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/>
    /// and the <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>: <c>hit</c> or <c>miss</c>.
    /// </summary>
    public static Counter<long> MatrixCacheLookups { get; } = Meter.CreateCounter<long>(
        "crestapps.geo.matrix.cache.lookups",
        "{lookup}",
        "The number of travel-time matrix lookups in the cache, by whether they were found.");

    /// <summary>
    /// The number of geocoding requests the background queue processed, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>: <see cref="GeocodeDone"/>, <see cref="GeocodeNoMatch"/>,
    /// <see cref="GeocodeGone"/>, <see cref="GeocodeNoTarget"/>, <see cref="GeocodeNoProvider"/>,
    /// <see cref="GeocodeRetry"/> or <see cref="GeocodeFailed"/>.
    /// </summary>
    public static Counter<long> GeocodeRequests { get; } = Meter.CreateCounter<long>(
        "crestapps.geo.geocode.requests",
        "{request}",
        "The number of geocoding requests the background queue processed, by what became of them.");

    /// <summary>
    /// Records a failed provider call answered with the built-in estimate.
    /// </summary>
    /// <param name="provider">The provider that failed.</param>
    /// <param name="operation">The operation, such as <see cref="OperationMatrix"/>.</param>
    public static void RecordFallback(string provider, string operation)
    {
        Add(Fallbacks, 1, CrestAppsOrchardCoreTelemetry.ProviderTag, provider, CrestAppsOrchardCoreTelemetry.OperationTag, operation);
    }

    /// <summary>
    /// Records a travel-time matrix lookup in the cache.
    /// </summary>
    /// <param name="provider">The provider whose answers are cached.</param>
    /// <param name="hit">Whether the matrix was found.</param>
    public static void RecordMatrixCacheLookup(string provider, bool hit)
    {
        Add(MatrixCacheLookups, 1, CrestAppsOrchardCoreTelemetry.ProviderTag, provider, CrestAppsOrchardCoreTelemetry.ResultTag, hit ? "hit" : "miss");
    }

    /// <summary>
    /// Records what became of a geocoding request.
    /// </summary>
    /// <param name="result">The result, such as <see cref="GeocodeDone"/>.</param>
    public static void RecordGeocodeRequest(string result)
    {
        if (!GeocodeRequests.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        GeocodeRequests.Add(1, tags);
    }

    private static void Add(Counter<long> counter, long value, string firstKey, string firstValue, string secondKey, string secondValue)
    {
        if (!counter.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { firstKey, firstValue ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { secondKey, secondValue ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(value, tags);
    }
}
