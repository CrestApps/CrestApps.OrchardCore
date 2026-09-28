using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// What the soft phone told the harness hub about the calls it placed itself: each call it recorded, each end it
/// reported, and each call it said was still up. A browser-originated call has no server-side call behind it, so these
/// reports are the only record the platform ever gets of it. The phone's client diagnostics are kept too, so a test
/// can tell how the phone learned a call was over.
/// </summary>
public sealed class BrowserCallLog
{
    private readonly ConcurrentQueue<string> _started = new();
    private readonly ConcurrentQueue<string> _ended = new();
    private readonly ConcurrentQueue<string> _alive = new();
    private readonly ConcurrentQueue<string> _diagnostics = new();

    public void Started(string callId) => _started.Enqueue(callId);

    public void Ended(string callId) => _ended.Enqueue(callId);

    public void Alive(IEnumerable<string> callIds)
    {
        foreach (var callId in callIds ?? [])
        {
            _alive.Enqueue(callId);
        }
    }

    public void Diagnostic(string code, string context) => _diagnostics.Enqueue(code + ":" + context);

    public BrowserCallLogSnapshot Snapshot() => new()
    {
        Started = [.. _started],
        Ended = [.. _ended],
        Alive = [.. _alive],
        Diagnostics = [.. _diagnostics],
    };
}

/// <summary>
/// A copy of the <see cref="BrowserCallLog"/>, as the page reads it back.
/// </summary>
public sealed class BrowserCallLogSnapshot
{
    public string[] Started { get; init; } = [];

    public string[] Ended { get; init; } = [];

    public string[] Alive { get; init; } = [];

    /// <summary>
    /// The client diagnostics the phone reported, as <c>code:context</c>.
    /// </summary>
    public string[] Diagnostics { get; init; } = [];
}
