using CrestApps.Core.ContactCenter.Models;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Plays whatever the queue's treatment policy says is due to the callers waiting in it.
/// </summary>
public interface IQueueTreatmentService
{
    /// <summary>
    /// Runs one treatment pass over a queue's waiting callers.
    /// </summary>
    /// <param name="queue">The queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many callers heard something.</returns>
    Task<int> RunDueAsync(ActivityQueue queue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the queue's hold music on one caller's leg.
    /// </summary>
    /// <remarks>
    /// For the caller a pass will never reach: the moment an agent is offered the call the item stops being a
    /// waiting one, and the pass only reads waiting items. That caller is still on the line for as long as the
    /// agent's phone rings — up to the queue's reservation timeout — and until this existed they spent all of it
    /// in silence, having just been told a person was coming. The music is stopped when somebody accepts.
    /// </remarks>
    /// <param name="queue">The queue whose hold music is played.</param>
    /// <param name="providerCallId">The caller's live leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task StartHoldMusicAsync(ActivityQueue queue, string providerCallId, CancellationToken cancellationToken = default);
}
