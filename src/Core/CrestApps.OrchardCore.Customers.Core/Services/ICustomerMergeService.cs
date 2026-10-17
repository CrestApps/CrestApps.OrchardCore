namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Joins a guest's history to their account: when a user's email is verified, the guest customers who bought with that
/// email merge into the user's customer.
/// </summary>
public interface ICustomerMergeService
{
    /// <summary>
    /// Merges the guest customers with the user's email into the user's customer, when the email is verified. Returns how
    /// many were merged.
    /// </summary>
    Task<int> MergeVerifiedGuestsAsync(string userId, CancellationToken cancellationToken = default);
}
