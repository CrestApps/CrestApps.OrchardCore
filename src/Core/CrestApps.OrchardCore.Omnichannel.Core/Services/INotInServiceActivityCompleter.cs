using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Completes an activity whose call found the number not in service, without an agent or the AI.
/// </summary>
/// <remarks>
/// A number that is not in service has nothing to say to an agent or an assistant, and nothing to retry. What the
/// attempt leaves behind is a completed activity in the contact's history, dispositioned as not in service, so a
/// report can count how many numbers a campaign found dead and how much of the dialer's work went on them. The
/// number is marked at the same time, so no later load or dial reaches it again.
/// </remarks>
public interface INotInServiceActivityCompleter
{
    /// <summary>
    /// Marks the number and completes the activity as not in service.
    /// </summary>
    /// <param name="request">The activity, the number and what said it is out of service.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of completing the activity.</returns>
    Task<ActivityDispositionResult> CompleteAsync(NotInServiceCompletionRequest request, CancellationToken cancellationToken = default);
}
