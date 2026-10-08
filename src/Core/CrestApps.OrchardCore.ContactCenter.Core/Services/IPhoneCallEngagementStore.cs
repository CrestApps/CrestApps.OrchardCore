using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Keeps the supervisor engagements on agents' own phone calls, for as long as the calls last.
/// </summary>
public interface IPhoneCallEngagementStore
{
    /// <summary>
    /// Lists the engagements on a call.
    /// </summary>
    /// <param name="callId">The call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The engagements, possibly none.</returns>
    Task<IReadOnlyList<PhoneCallEngagement>> ListAsync(string callId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the engagement of one supervisor on a call.
    /// </summary>
    /// <param name="callId">The call.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The engagement, or <see langword="null"/>.</returns>
    Task<PhoneCallEngagement> FindAsync(string callId, string supervisorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the engagement a supervisor's leg belongs to.
    /// </summary>
    /// <param name="supervisorLegId">The supervisor's leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The engagement, or <see langword="null"/>.</returns>
    Task<PhoneCallEngagement> FindBySupervisorLegAsync(string supervisorLegId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or replaces an engagement.
    /// </summary>
    /// <param name="engagement">The engagement.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SaveAsync(PhoneCallEngagement engagement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets an engagement.
    /// </summary>
    /// <param name="engagement">The engagement.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RemoveAsync(PhoneCallEngagement engagement, CancellationToken cancellationToken = default);
}
