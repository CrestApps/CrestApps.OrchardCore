using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Runs the work a finished live call left behind, away from the scope the call ran in.
/// </summary>
/// <remarks>
/// This exists because of where a realtime session runs: inside the provider's "call answered" webhook request,
/// for the entire length of the call. No provider waits that long for a webhook response — it times out, retries,
/// and the connection is aborted — so by the time the session ends, the request, its cancellation token and its
/// services are gone. Work left in that scope disappears without a trace, which is exactly what happened to a
/// caller who asked for an agent: the transfer was recorded and nobody was ever enqueued.
/// </remarks>
public interface IRealtimeCallCompletionRunner
{
    /// <summary>
    /// Carries out what the session decided, in a scope of its own.
    /// </summary>
    /// <param name="completion">What the session decided, and the call it decided it about.</param>
    Task RunAsync(RealtimeCallCompletion completion);
}
