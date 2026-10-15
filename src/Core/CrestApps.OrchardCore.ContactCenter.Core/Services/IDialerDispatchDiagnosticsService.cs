using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Explains why queued work is not being handed to agents. It reads what the dispatcher already decides -- whether the queue
/// is running, whether a campaign record may be dialed yet, why the compliance gate suppressed a dial and what routing
/// found when it looked for an agent -- and changes nothing.
/// </summary>
public interface IDialerDispatchDiagnosticsService
{
    /// <summary>
    /// Diagnoses the supplied queues.
    /// </summary>
    /// <param name="queueIds">The queues to diagnose: stored queues and the virtual queues campaigns run under.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>One diagnosis per queue, in the order the queues were supplied.</returns>
    Task<IReadOnlyList<QueueDispatchDiagnostics>> DiagnoseAsync(
        IReadOnlyCollection<string> queueIds,
        CancellationToken cancellationToken = default);
}
