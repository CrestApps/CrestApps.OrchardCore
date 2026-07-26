using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Records outbound HTTP requests issued through <see cref="HttpClient"/> anywhere in the process while the
/// recorder is active.
/// </summary>
/// <remarks>
/// Observation is done through the <see cref="DiagnosticListener"/> that <see cref="HttpClient"/> writes to, rather
/// than by replacing a handler in the container. A test that swaps a handler only sees the clients it managed to
/// intercept, so a regression that builds its own <see cref="HttpClient"/> would slip past unnoticed. The diagnostic
/// source is written by the framework itself on every request, so no way of *constructing* a client avoids being
/// recorded.
/// <para>
/// The guarantee is bounded, and stating it precisely matters. Coverage is complete for egress that flows through
/// the framework's HTTP message pipeline, which is how every realistic search-client regression would reach a
/// cluster. It does not extend to a custom transport, a hand-written handler that bypasses the instrumented send
/// path, or raw socket code. Closing that remaining gap needs network isolation around the test process rather
/// than in-process observation, which is tracked as the packaging-harness follow-up.
/// </para>
/// <para>
/// The recorder deliberately captures all destinations rather than filtering for known search endpoints. Matching on
/// host names would only catch a regression that names its search cluster recognizably, and a deployment is free to
/// call its cluster anything. A supported single-node correctness path has no legitimate out-of-process dependency
/// at all, so the absence of any outbound request is both the stronger property and the simpler one to state.
/// </para>
/// </remarks>
public sealed class HttpEgressRecorder : IDisposable, IObserver<DiagnosticListener>
{
    private const string HttpListenerName = "HttpHandlerDiagnosticListener";
    private const string RequestStartEventName = "System.Net.Http.HttpRequestOut.Start";

    private readonly ConcurrentQueue<string> _observed = new();
    private readonly List<IDisposable> _subscriptions = [];
    private readonly IDisposable _allListeners;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpEgressRecorder"/> class and begins recording.
    /// </summary>
    public HttpEgressRecorder()
    {
        _allListeners = DiagnosticListener.AllListeners.Subscribe(this);
    }

    /// <summary>
    /// Proves the recorder is attached by issuing a request and requiring it to be observed.
    /// </summary>
    /// <returns>A task that completes once the recorder has been confirmed to be observing.</returns>
    /// <remarks>
    /// Attachment depends on a process-wide diagnostic listener that another test may already have created, and a
    /// recorder that silently observed nothing would turn its assertion into a guaranteed pass. Rather than reason
    /// about subscription semantics and test ordering, this issues a controlled request, requires it to appear, and
    /// then clears it, so the caller measures the recorder instead of assuming it works.
    /// </remarks>
    public async Task EnsureObservingAsync()
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(250),
        };

        try
        {
            // The destination only has to be attempted, never reached: the diagnostic event is written when the
            // request starts, so a refused connection still proves the recorder is attached.
            await client.GetAsync(new Uri("http://127.0.0.1:1/egress-recorder-self-test"));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
        }

        if (_observed.IsEmpty)
        {
            throw new InvalidOperationException(
                "The outbound HTTP recorder observed nothing after issuing a request to itself, so it would have " +
                "reported an empty result no matter what the exercised code did. Attach the recorder before any " +
                "HTTP diagnostic listener is created, or run the check in an isolated process.");
        }

        _observed.Clear();
    }

    /// <summary>
    /// Gets the outbound requests observed since the recorder was created.
    /// </summary>
    /// <returns>A description of each observed request, in no particular order.</returns>
    public IReadOnlyCollection<string> GetObservedRequests()
        => [.. _observed.Order(StringComparer.Ordinal)];

    /// <inheritdoc/>
    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name != HttpListenerName)
        {
            return;
        }

        lock (_subscriptions)
        {
            _subscriptions.Add(listener.Subscribe(new RequestObserver(_observed)));
        }
    }

    /// <inheritdoc/>
    public void OnCompleted()
    {
    }

    /// <inheritdoc/>
    public void OnError(Exception error)
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _allListeners.Dispose();

        lock (_subscriptions)
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }
    }

    private sealed class RequestObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly ConcurrentQueue<string> _observed;

        public RequestObserver(ConcurrentQueue<string> observed)
        {
            _observed = observed;
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key != RequestStartEventName || value.Value is null)
            {
                return;
            }

            // The payload is an anonymous type, so the request has to be read reflectively. A payload shape change
            // upstream must not silently turn the recorder into a no-op, so an unreadable payload is still recorded.
            var property = value.Value.GetType().GetProperty("Request");
            var request = property?.GetValue(value.Value) as HttpRequestMessage;

            _observed.Enqueue(request is null
                ? "an outbound HTTP request whose destination could not be read from the diagnostic payload"
                : $"{request.Method} {request.RequestUri}");
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
