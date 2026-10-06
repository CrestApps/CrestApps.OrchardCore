using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Completes a dialer activity whose attempt ended before any agent was connected, with the disposition for how it
/// ended, so the agent the dialer reserved never receives it as work.
/// </summary>
/// <remarks>
/// It is the one place every pre-connect ending goes through -- a call that rang out, was busy, rejected or failed, a
/// machine that answered, a customer who hung up while the agent was being joined, a dial the provider refused, and an
/// activity found to have used every attempt -- so they all end the same way: routing lets go of the agent first,
/// then the activity is completed as the system, and the disposition's subject actions and the
/// <c>ActivityDispositionApplied</c> event decide whether and when to call again.
/// </remarks>
public interface IDialerAttemptFinalizer
{
    /// <summary>
    /// Completes the activity of a pre-connect dialer attempt with the disposition for its outcome.
    /// </summary>
    /// <param name="request">The activity, the attempt and how it ended.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What became of the activity.</returns>
    Task<DialerAttemptFinalizationResult> FinalizeAsync(DialerAttemptFinalizationRequest request, CancellationToken cancellationToken = default);
}
