using CrestApps.Core.Telephony.Models;

namespace CrestApps.Core.Telephony;

/// <summary>
/// Executes the conference operations a telephony provider supports.
/// </summary>
public interface ITelephonyConferenceProvider
{
    /// <summary>
    /// Merges two active calls into a single conference.
    /// </summary>
    /// <param name="request">The merge request describing the calls to join.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default);
}
