using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Tests.Telemetry.ContactCenter;

/// <summary>
/// Captures what the CrestApps Orchard Core meters and activity sources emit while one test runs. The Contact Center
/// and webhook instruments carry only bounded tags, so a test cannot tell its measurements apart by a tag value; it
/// runs under a root span of its own instead, and only the measurements and spans of that trace are kept.
/// </summary>
internal sealed class TraceScopedTelemetryCapture : IDisposable
{
    private readonly Activity _root;
    private readonly MeterListener _meterListener;
    private readonly ActivityListener _activityListener;

    public TraceScopedTelemetryCapture()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(CrestAppsOrchardCoreTelemetry.SourceNamePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == TraceId)
                {
                    Spans.Enqueue(activity);
                }
            },
        };

        ActivitySource.AddActivityListener(_activityListener);

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
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
        _meterListener.Start();

        _root = new Activity("contactcenter-telemetry-test").Start();
        TraceId = _root.TraceId;
    }

    public ActivityTraceId TraceId { get; }

    public ConcurrentQueue<(string Instrument, double Value, Dictionary<string, object> Tags)> Measurements { get; } = new();

    public ConcurrentQueue<Activity> Spans { get; } = new();

    /// <summary>
    /// Gets the measurements this test's trace recorded on an instrument.
    /// </summary>
    public IReadOnlyList<(string Instrument, double Value, Dictionary<string, object> Tags)> Find(string instrument)
        => Measurements.Where(measurement => measurement.Instrument == instrument).ToList();

    /// <summary>
    /// Collects the observable instruments now, on this test's trace.
    /// </summary>
    public void RecordObservableInstruments()
        => _meterListener.RecordObservableInstruments();

    /// <summary>
    /// Gets the tag keys of the captured measurements that a metric must never carry.
    /// </summary>
    public IReadOnlyList<string> FindBannedTagKeys()
        => Measurements
            .SelectMany(measurement => measurement.Tags.Keys)
            .Where(CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public void Dispose()
    {
        _root.Dispose();
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    private void Add<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        where T : struct
    {
        if (Activity.Current?.TraceId != TraceId)
        {
            return;
        }

        var values = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var tag in tags)
        {
            values[tag.Key] = tag.Value;
        }

        Measurements.Enqueue((instrument.Name, Convert.ToDouble(value), values));
    }
}
