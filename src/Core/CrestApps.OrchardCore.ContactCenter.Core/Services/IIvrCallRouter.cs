using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Carries out what an entry point's phone menu decided: starts the menu for a new caller, and puts a caller who
/// has chosen through to the queue, agent, voicemail box or external number they chose.
/// </summary>
public interface IIvrCallRouter
{
    /// <summary>
    /// Answers the caller and plays the entry point's first menu. A caller the menu cannot be played to is put
    /// through to the entry point's own target instead.
    /// </summary>
    /// <param name="interactionId">The caller's interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task StartAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts the caller through to where the menu's step says. A menu step, or a step for a caller who is no longer
    /// in the menu, does nothing.
    /// </summary>
    /// <param name="interactionId">The caller's interaction.</param>
    /// <param name="entryPoint">The entry point whose menu decided the step.</param>
    /// <param name="step">What the menu decided.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RouteAsync(string interactionId, ContactCenterEntryPoint entryPoint, IvrStep step, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a caller whose transfer to an outside number failed where the menu's fallback sends callers, or, when the
    /// fallback is a menu or is the same number that just failed, through to the entry point's own target.
    /// </summary>
    /// <param name="interactionId">The caller's interaction.</param>
    /// <param name="failedDestinationId">The approved destination that did not answer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecoverFailedTransferAsync(string interactionId, string failedDestinationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers the caller and says the entry point's welcome or closed message the routing decided they are owed.
    /// Once it has been said (<see cref="CompleteAnnouncementAsync"/>) they go on to the menu, the entry point's target,
    /// the queue a closed entry point holds them in, voicemail, or the end of the call. A caller the message cannot be
    /// said to goes on at once, as if no message were configured. A caller owed no message, or who has already had it,
    /// is left alone.
    /// </summary>
    /// <param name="interactionId">The caller's interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task AnnounceAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves on a caller whose welcome or closed message has just been said.
    /// </summary>
    /// <param name="interactionId">The caller's interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the caller was waiting on the message and has been moved on.</returns>
    Task<bool> CompleteAnnouncementAsync(string interactionId, CancellationToken cancellationToken = default);
}
