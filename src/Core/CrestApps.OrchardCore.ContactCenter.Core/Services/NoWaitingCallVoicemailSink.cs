using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The sink a tenant without a voice feature gets: nothing can move a live call, so nobody is moved. The queue
/// limit that asked reports the caller as still waiting rather than failing the sweep.
/// </summary>
public sealed class NoWaitingCallVoicemailSink : IWaitingCallVoicemailSink
{
    /// <inheritdoc/>
    public Task<bool> SendToVoicemailAsync(QueueItem item, string reasonCode, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
