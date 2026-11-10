using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Tests.Telemetry;

/// <summary>
/// Listens to the CrestApps Orchard Core meters and activity sources while a test runs, and keeps what they emit.
/// The meters are process-wide and tests run in parallel, so a test filters the captured measurements by a tag value
/// that only it uses.
/// </summary>
internal sealed class TelemetryCapture : IDisposable
{
    private readonly MeterListener _meterListener;
    private readonly ActivityListener _activityListener;

    public TelemetryCapture()
    {
        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name.StartsWith(CrestAppsOrchardCoreTelemetry.SourceNamePrefix, StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => Add(instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
        _meterListener.Start();

        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(CrestAppsOrchardCoreTelemetry.SourceNamePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Spans.Enqueue,
        };

        ActivitySource.AddActivityListener(_activityListener);
    }

    public ConcurrentQueue<CapturedMeasurement> Measurements { get; } = new();

    public ConcurrentQueue<Activity> Spans { get; } = new();

    /// <summary>
    /// Gets the measurements of an instrument whose tag <paramref name="key"/> equals <paramref name="value"/>.
    /// </summary>
    public IReadOnlyList<CapturedMeasurement> Find(string instrument, string key, object value)
    {
        return Measurements
            .Where(measurement => measurement.Instrument == instrument && Equals(measurement.Tags.GetValueOrDefault(key), value))
            .ToList();
    }

    /// <summary>
    /// Gets the tag keys of the captured measurements that a metric must never carry.
    /// </summary>
    public IReadOnlyList<string> FindBannedTagKeys()
    {
        return Measurements
            .SelectMany(measurement => measurement.Tags.Keys)
            .Where(CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    private void Add<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        where T : struct
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var tag in tags)
        {
            values[tag.Key] = tag.Value;
        }

        Measurements.Enqueue(new CapturedMeasurement(instrument.Meter.Name, instrument.Name, Convert.ToDouble(value), values));
    }
}

/// <summary>
/// One measurement captured by <see cref="TelemetryCapture"/>.
/// </summary>
internal sealed record CapturedMeasurement(string Meter, string Instrument, double Value, Dictionary<string, object> Tags);
