using System.Diagnostics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;

/// <summary>
/// Measures one messaging operation that ends with a bounded result, such as a send, an inbound message or an
/// automated turn: a span and a duration histogram tagged with the outcome and the result, both
/// recorded when the measurement is disposed. Start it with one of the <c>Start</c> methods of
/// <see cref="MessagingTelemetry"/> in a <c>using</c> declaration.
/// </summary>
/// <remarks>
/// A measurement that is disposed before <see cref="Complete(string, string)"/> or <see cref="Fail(Exception)"/> is
/// called records the <see cref="MessagingTelemetry.ResultError"/> result, since the operation never said how it
/// ended.
/// </remarks>
public sealed class MessagingMeasurement : IDisposable
{
    private readonly TelemetryScope _scope;
    private string _result;
    private bool _disposed;

    internal MessagingMeasurement(TelemetryScope scope)
    {
        _scope = scope;
    }

    /// <summary>
    /// Gets the span of the operation, or <see langword="null"/> when nothing listens to the messaging source.
    /// </summary>
    public Activity Activity => _scope.Activity;

    /// <summary>
    /// Gets the result of the operation so far: <see cref="MessagingTelemetry.ResultError"/> until
    /// <see cref="Complete(string, string)"/> says otherwise.
    /// </summary>
    public string Result => _result ?? MessagingTelemetry.ResultError;

    /// <summary>
    /// Gets a value indicating whether the operation already said how it ended, so an exception thrown after that,
    /// such as one thrown to have a delivery retried, keeps the result that was recorded.
    /// </summary>
    public bool IsCompleted => _result is not null;

    /// <summary>
    /// Gets the time elapsed since the operation started.
    /// </summary>
    public TimeSpan Elapsed => _scope.Elapsed;

    /// <summary>
    /// Adds a bounded tag to the duration histogram and the span, such as the provider that sent a
    /// message once it is known.
    /// </summary>
    /// <param name="key">The tag key, never one of <see cref="CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys"/>.</param>
    /// <param name="value">The tag value, from a bounded set.</param>
    public void AddTag(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        _scope.AddMetricTag(key, value);
    }

    /// <summary>
    /// Sets a tag on the span only, such as the id of the conversation the operation worked on.
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    public void SetSpanTag(string key, object value)
    {
        _scope.SetSpanTag(key, value);
    }

    /// <summary>
    /// Records how the operation ended. Call it once.
    /// </summary>
    /// <param name="result">The result, from the bounded set the instrument documents.</param>
    /// <param name="outcome">The outcome of the duration histogram: <see cref="CrestAppsOrchardCoreTelemetry.OutcomeOk"/>,
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeSkipped"/>, <see cref="CrestAppsOrchardCoreTelemetry.OutcomeCancelled"/>,
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTimeout"/> or <see cref="CrestAppsOrchardCoreTelemetry.OutcomeError"/>.
    /// An error also marks the span as failed.</param>
    public void Complete(string result, string outcome = CrestAppsOrchardCoreTelemetry.OutcomeOk)
    {
        ArgumentException.ThrowIfNullOrEmpty(result);
        ArgumentException.ThrowIfNullOrEmpty(outcome);

        _result = result;
        _scope.SetSpanTag(MessagingTelemetry.ResultSpanTag, result);

        if (outcome == CrestAppsOrchardCoreTelemetry.OutcomeError)
        {
            _scope.Fail();

            return;
        }

        _scope.SetOutcome(outcome);
    }

    /// <summary>
    /// Records that the operation ended with an exception: the <see cref="MessagingTelemetry.ResultError"/> result, or
    /// the <see cref="MessagingTelemetry.ResultCancelled"/> result for a cancellation.
    /// </summary>
    /// <remarks>
    /// The exception type is recorded, but neither its message nor its stack: a provider's or a database's error
    /// message can carry a phone number or a message body, and a span must carry neither. Whoever handles the exception
    /// still logs it.
    /// </remarks>
    /// <param name="exception">The exception the operation ended with.</param>
    public void Fail(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var outcome = CrestAppsOrchardCoreTelemetry.GetOutcome(exception);

        _result = outcome == CrestAppsOrchardCoreTelemetry.OutcomeCancelled
            ? MessagingTelemetry.ResultCancelled
            : MessagingTelemetry.ResultError;

        _scope.SetSpanTag(MessagingTelemetry.ResultSpanTag, _result);

        if (outcome != CrestAppsOrchardCoreTelemetry.OutcomeError)
        {
            _scope.SetOutcome(outcome);

            return;
        }

        _scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ErrorTypeTag, exception.GetType().FullName);
        _scope.Fail();
    }

    /// <summary>
    /// Records the duration, the outcome and the result, and ends the span.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ResultTag, Result);

        _scope.Dispose();
    }
}
