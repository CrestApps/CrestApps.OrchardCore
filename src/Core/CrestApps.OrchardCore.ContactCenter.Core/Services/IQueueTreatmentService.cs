using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

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

    /// <summary>
    /// Gives a caller the platform has already answered something to hear while they wait for a person: the queue's
    /// hold music when it has some, and otherwise a ringing tone.
    /// </summary>
    /// <remarks>
    /// A caller answered to hear a phone menu who chose an agent, or a queue with no music, heard nothing at all while
    /// the agent's phone rang: the network's ringback ended when the menu answered them. Whatever is started here is
    /// stopped the way hold music is — when the agent is joined, or when the caller leaves the queue for voicemail or
    /// anywhere else.
    /// </remarks>
    /// <param name="queue">The queue whose music is played, or <see langword="null"/> when there is none, such as a
    /// call ringing one agent directly.</param>
    /// <param name="providerCallId">The caller's live leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task StartWaitingAudioAsync(ActivityQueue queue, string providerCallId, CancellationToken cancellationToken = default);
}
