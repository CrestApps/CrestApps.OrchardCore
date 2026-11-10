using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Measures one operation: a span, a duration histogram and how the operation ended, recorded together when the
/// scope is disposed. Start it with <see cref="Start(ActivitySource, string, Histogram{double}, TagList, ActivityKind)"/>
/// in a <c>using</c> declaration, call <see cref="Succeed"/> or <see cref="Fail(Exception)"/>, and let the scope
/// record the rest. Nothing is recorded, and next to nothing is spent, while no listener is attached.
/// </summary>
/// <remarks>
/// The duration histogram carries the tags given to <see cref="Start(ActivitySource, string, Histogram{double}, TagList, ActivityKind)"/>,
/// the tags added with <see cref="AddMetricTag(string, object)"/>, and the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
/// Those tags must be bounded; ids go on the span only, with <see cref="SetSpanTag(string, object)"/>. A scope that
/// is disposed before <see cref="Succeed"/>, <see cref="SetOutcome(string)"/> or <see cref="Fail(Exception)"/> is
/// called, such as on an early <c>return</c>, records the <see cref="CrestAppsOrchardCoreTelemetry.Unknown"/> outcome
/// and leaves the span status unset: the operation said neither that it succeeded nor that it failed, so the metric and
/// the span agree and an early return is not counted as a failure. Set an outcome on every path.
/// </remarks>
public sealed class TelemetryScope : IDisposable
{
    private readonly Histogram<double> _duration;
    private readonly long _startTimestamp;
    private TagList _tags;
    private string _outcome;
    private bool _disposed;

    private TelemetryScope(Activity activity, Histogram<double> duration, TagList tags)
    {
        Activity = activity;
        _duration = duration;
        _tags = tags;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Gets the span of the operation, or <see langword="null"/> when nothing listens to its source.
    /// </summary>
    public Activity Activity { get; }

    /// <summary>
    /// Gets the time elapsed since the operation started.
    /// </summary>
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);

    /// <summary>
    /// Gets how the operation ended so far: <see cref="CrestAppsOrchardCoreTelemetry.Unknown"/> until
    /// <see cref="Succeed"/>, <see cref="SetOutcome(string)"/> or <see cref="Fail(Exception)"/> says otherwise.
    /// </summary>
    public string Outcome => _outcome ?? CrestAppsOrchardCoreTelemetry.Unknown;

    /// <summary>
    /// Starts measuring an operation.
    /// </summary>
    /// <param name="source">The activity source of the area.</param>
    /// <param name="spanName">The name of the span, such as <c>crestapps.contenttransfer.import.run</c>.</param>
    /// <param name="duration">The duration histogram, or <see langword="null"/> to record a span only.</param>
    /// <param name="tags">The bounded tags of the duration histogram, which are also set on the span.</param>
    /// <param name="kind">The kind of the span, <see cref="ActivityKind.Internal"/> by default. A span of ours stays internal
    /// even when the operation calls another system: HttpClient instrumentation starts the client span itself.</param>
    /// <returns>The scope.</returns>
    public static TelemetryScope Start(
        ActivitySource source,
        string spanName,
        Histogram<double> duration,
        TagList tags = default,
        ActivityKind kind = ActivityKind.Internal)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(spanName);

        var activity = source.StartActivity(spanName, kind);

        if (activity is not null)
        {
            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }

        return new TelemetryScope(activity, duration, tags);
    }

    /// <summary>
    /// Starts measuring an operation whose span starts a new trace, linked to the trace that queued the work, such
    /// as a background import of a file uploaded by an earlier request.
    /// </summary>
    /// <param name="source">The activity source of the area.</param>
    /// <param name="spanName">The name of the span.</param>
    /// <param name="duration">The duration histogram, or <see langword="null"/> to record a span only.</param>
    /// <param name="traceParent">The W3C <c>traceparent</c> captured with <see cref="TraceContextPropagation.Capture"/> when the work was queued.</param>
    /// <param name="tags">The bounded tags of the duration histogram, which are also set on the span.</param>
    /// <returns>The scope.</returns>
    public static TelemetryScope StartLinked(
        ActivitySource source,
        string spanName,
        Histogram<double> duration,
        string traceParent,
        TagList tags = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(spanName);

        var activity = TraceContextPropagation.StartLinkedActivity(source, spanName, traceParent);

        if (activity is not null)
        {
            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }

        return new TelemetryScope(activity, duration, tags);
    }

    /// <summary>
    /// Adds a bounded tag to the duration histogram and to the span, such as the status class of a response.
    /// </summary>
    /// <param name="key">The tag key, never one of <see cref="CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys"/>.</param>
    /// <param name="value">The tag value, from a bounded set.</param>
    public void AddMetricTag(string key, object value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        _tags.Add(key, value);
        Activity?.SetTag(key, value);
    }

    /// <summary>
    /// Sets a tag on the span only, such as the id of the record the operation worked on.
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    public void SetSpanTag(string key, object value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        Activity?.SetTag(key, value);
    }

    /// <summary>
    /// Marks the operation as completed.
    /// </summary>
    public void Succeed()
    {
        SetOutcome(CrestAppsOrchardCoreTelemetry.OutcomeOk);
    }

    /// <summary>
    /// Sets how the operation ended, such as <see cref="CrestAppsOrchardCoreTelemetry.OutcomeSkipped"/> or a value the
    /// area documents.
    /// </summary>
    /// <param name="outcome">The outcome, from a bounded set.</param>
    public void SetOutcome(string outcome)
    {
        ArgumentException.ThrowIfNullOrEmpty(outcome);

        _outcome = outcome;
    }

    /// <summary>
    /// Marks the operation as failed by an exception: <see cref="CrestAppsOrchardCoreTelemetry.OutcomeCancelled"/> for a
    /// cancellation, <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTimeout"/> for a timeout, otherwise
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeError"/> with the exception type.
    /// </summary>
    /// <param name="exception">The exception the operation ended with.</param>
    public void Fail(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _outcome = CrestAppsOrchardCoreTelemetry.GetOutcome(exception);

        if (_outcome == CrestAppsOrchardCoreTelemetry.OutcomeError)
        {
            _tags.Add(CrestAppsOrchardCoreTelemetry.ErrorTypeTag, exception.GetType().FullName);
        }

        CrestAppsOrchardCoreTelemetry.RecordException(Activity, exception);
    }

    /// <summary>
    /// Marks the operation as failed without an exception, such as a call that returned an error response or a
    /// failure that was logged and swallowed.
    /// </summary>
    /// <param name="reason">A reason from a bounded set of constants (never free text such as an error message),
    /// recorded on the span and the duration histogram.</param>
    public void Fail(string reason = null)
    {
        _outcome = CrestAppsOrchardCoreTelemetry.OutcomeError;

        if (!string.IsNullOrEmpty(reason))
        {
            AddMetricTag(CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        }

        Activity?.SetStatus(ActivityStatusCode.Error, reason);
    }

    /// <summary>
    /// Records the duration and the outcome, and ends the span.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var outcome = Outcome;

        if (_duration is not null && _duration.Enabled)
        {
            var tags = _tags;
            tags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome);
            CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
            _duration.Record(Elapsed.TotalSeconds, tags);
        }

        if (Activity is not null)
        {
            Activity.SetTag(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome);
            Activity.Dispose();
        }
    }
}
