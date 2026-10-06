using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Completes an activity with the disposition that stands for an outcome the platform reached on its own, such as a
/// dialed call nobody answered, without an agent or the AI.
/// </summary>
/// <remarks>
/// The disposition is chosen by its <see cref="DispositionOutcome"/>, preferring one the subject's flow is wired to so
/// the subject's actions run -- a "Try again" action is what schedules the next attempt. A tenant with no disposition
/// for the outcome still gets one, created the first time it is needed, because a completed call with no disposition
/// is invisible to every report that counts outcomes and a subject that requires one would refuse the completion.
/// </remarks>
public interface IActivityOutcomeCompleter
{
    /// <summary>
    /// Completes the activity with the disposition for the outcome.
    /// </summary>
    /// <param name="request">The activity, the outcome and what to record with it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of completing the activity.</returns>
    Task<ActivityDispositionResult> CompleteAsync(ActivityOutcomeCompletionRequest request, CancellationToken cancellationToken = default);
}
