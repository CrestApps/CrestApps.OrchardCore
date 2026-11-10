using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Geo.Core.Telemetry;

/// <summary>
/// The meter and activity source of geo services, <c>CrestApps.OrchardCore.Geo</c>: the calls to the geocoding,
/// travel-time and route providers, the travel-time cache, the fallbacks to the built-in estimate and the background
/// geocoding queue. Addresses, coordinates and record ids never go on a metric.
/// </summary>
public static class GeoTelemetry
{
    /// <summary>
    /// The name of the geo meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Geo";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> of finding the places that match an address.
    /// </summary>
    public const string OperationGeocode = "geocode";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> of finding the address of a point.
    /// </summary>
    public const string OperationReverseGeocode = "reverse_geocode";

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
    /// The activity source of geo services.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Geo");

    /// <summary>
    /// The duration of a call to a geo provider, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/>,
    /// the <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> and the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// </summary>
    public static Histogram<double> ProviderDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.geo.provider.duration",
        "The duration of a call to a geocoding, travel-time or route provider.");

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
    /// The number of geocoding requests waiting in the queue, read once per run of the queue. Without the tenant tag,
    /// the tenants of a node report into the same series, so the last tenant to run wins.
    /// </summary>
    public static Gauge<long> GeocodeQueueDepth { get; } = Meter.CreateGauge<long>(
        "crestapps.geo.geocode.queue.depth",
        "{request}",
        "The number of geocoding requests waiting in the queue.");

    /// <summary>
    /// Starts measuring a call to a geo provider.
    /// </summary>
    /// <param name="provider">The provider's name.</param>
    /// <param name="operation">The operation, such as <see cref="OperationGeocode"/>.</param>
    /// <returns>The scope of the call.</returns>
    public static TelemetryScope StartProviderCall(string provider, string operation)
    {
        return ExternalCallTelemetry.Start(ActivitySource, "crestapps.geo." + operation, ProviderDuration, provider, operation);
    }

    /// <summary>
    /// Calls a geo provider and measures the call.
    /// </summary>
    /// <typeparam name="T">The type of the provider's answer.</typeparam>
    /// <param name="provider">The provider's name.</param>
    /// <param name="operation">The operation, such as <see cref="OperationGeocode"/>.</param>
    /// <param name="call">The call.</param>
    /// <returns>The provider's answer. Exceptions are recorded and rethrown.</returns>
    public static async Task<T> MeasureProviderCallAsync<T>(string provider, string operation, Func<Task<T>> call)
    {
        ArgumentNullException.ThrowIfNull(call);

        using var telemetry = StartProviderCall(provider, operation);

        try
        {
            var result = await call();
            telemetry.Succeed();

            return result;
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }
    }

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

    /// <summary>
    /// Records the number of geocoding requests waiting in the queue.
    /// </summary>
    /// <param name="depth">The number of requests.</param>
    public static void RecordGeocodeQueueDepth(long depth)
    {
        var tags = new TagList();

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        GeocodeQueueDepth.Record(depth, tags);
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
