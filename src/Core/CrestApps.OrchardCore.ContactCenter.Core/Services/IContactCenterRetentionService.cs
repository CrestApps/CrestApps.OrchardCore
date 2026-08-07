using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Enforces Contact Center data-governance retention by draining every high-volume table of records that have
/// aged past their configured window.
/// </summary>
public interface IContactCenterRetentionService
{
    /// <summary>
    /// Runs one retention cycle across every registered retention policy, draining each entity until it is
    /// empty or the cycle budget is exhausted.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A report describing what each entity purged and whether the cycle returned the database to steady state.
    /// </returns>
    Task<ContactCenterRetentionReport> PurgeAsync(CancellationToken cancellationToken = default);
}
