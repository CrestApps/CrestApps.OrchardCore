using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.WebSockets.Telemetry;

/// <summary>
/// The OpenTelemetry metrics of the WebSocket rendezvous, which hands a socket a provider dials back in (a call's media
/// stream) to the request that asked for it. The meter is named <see cref="MeterName"/>. The rendezvous key is
/// unguessable and never recorded.
/// </summary>
public static class WebSocketsTelemetry
{
    /// <summary>
    /// The name of the meter.
    /// </summary>
    public const string MeterName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".WebSockets";

    /// <summary>
    /// A rendezvous was registered and waits for its socket.
    /// </summary>
    public const string ResultRegistered = "registered";

    /// <summary>
    /// A rendezvous could not be registered, because its key was already pending here or on another node.
    /// </summary>
    public const string ResultConflict = "conflict";

    /// <summary>
    /// A socket arrived and was handed to its rendezvous.
    /// </summary>
    public const string ResultClaimed = "claimed";

    /// <summary>
    /// A socket arrived for a key no node is waiting on: unknown, expired or already claimed.
    /// </summary>
    public const string ResultMiss = "miss";

    /// <summary>
    /// A socket arrived on a node other than the one waiting for it, so it could not be handed over.
    /// </summary>
    public const string ResultCrossNode = "cross_node";

    /// <summary>
    /// A rendezvous was withdrawn before a socket arrived for it.
    /// </summary>
    public const string ResultRemoved = "removed";

    /// <summary>
    /// Gets the meter of the WebSocket rendezvous.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("WebSockets");

    /// <summary>
    /// The number of rendezvous operations, tagged with the <c>result</c>: <see cref="ResultRegistered"/>,
    /// <see cref="ResultConflict"/>, <see cref="ResultClaimed"/>, <see cref="ResultMiss"/>, <see cref="ResultCrossNode"/>
    /// or <see cref="ResultRemoved"/>.
    /// </summary>
    public static Counter<long> Rendezvous { get; } = Meter.CreateCounter<long>(
        "crestapps.websockets.rendezvous",
        "{rendezvous}",
        "The number of WebSocket rendezvous operations, by result.");

    /// <summary>
    /// The time from a rendezvous being registered to its socket arriving.
    /// </summary>
    public static Histogram<double> RendezvousWaitDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.websockets.rendezvous.wait.duration",
        "The time from a WebSocket rendezvous being registered to the provider's socket arriving for it.");

    // Kept whether or not anything listens, so the gauge reads the true number from the moment a listener attaches.
    private static long _pending;

    /// <summary>
    /// The number of rendezvous waiting for their socket in this process, across tenants.
    /// </summary>
    public static ObservableUpDownCounter<long> RendezvousPending { get; } = Meter.CreateObservableUpDownCounter(
        "crestapps.websockets.rendezvous.pending",
        () => Interlocked.Read(ref _pending),
        "{rendezvous}",
        "The number of WebSocket rendezvous waiting for their socket in this process.");

    /// <summary>
    /// Records a rendezvous operation, and keeps <see cref="RendezvousPending"/> in step with it.
    /// </summary>
    /// <param name="result">What happened, such as <see cref="ResultClaimed"/>.</param>
    /// <param name="registeredTimestamp">The <see cref="Stopwatch"/> timestamp the rendezvous was registered at, for a
    /// claim; otherwise zero.</param>
    public static void Record(string result, long registeredTimestamp = 0)
    {
        var tags = new TagList();
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        if (Rendezvous.Enabled)
        {
            var resultTags = tags;
            resultTags.Add(CrestAppsOrchardCoreTelemetry.ResultTag, result);
            Rendezvous.Add(1, resultTags);
        }

        var pendingDelta = result switch
        {
            ResultRegistered => 1,
            ResultClaimed or ResultRemoved => -1,
            _ => 0,
        };

        if (pendingDelta != 0)
        {
            Interlocked.Add(ref _pending, pendingDelta);
        }

        if (result == ResultClaimed && registeredTimestamp != 0 && RendezvousWaitDuration.Enabled)
        {
            RendezvousWaitDuration.Record(Stopwatch.GetElapsedTime(registeredTimestamp).TotalSeconds, tags);
        }
    }
}
