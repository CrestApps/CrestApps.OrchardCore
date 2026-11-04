namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Removes the personal details of old guest records a module keeps (such as guest orders) when the Customers data
/// retention window for guests' records runs out. The module that keeps the records implements it, so Customers applies
/// its retention settings without referencing that module.
/// </summary>
public interface IGuestRetentionHandler
{
    /// <summary>
    /// Anonymizes the module's settled guest records made before a time, at most <paramref name="limit"/> of them.
    /// </summary>
    /// <param name="createdBeforeUtc">Records made before this time are anonymized.</param>
    /// <param name="limit">The most records anonymized in one run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many records were anonymized.</returns>
    Task<int> AnonymizeGuestRecordsAsync(DateTime createdBeforeUtc, int limit, CancellationToken cancellationToken = default);
}
