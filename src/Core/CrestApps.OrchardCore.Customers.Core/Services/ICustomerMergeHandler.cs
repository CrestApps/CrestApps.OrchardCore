namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Moves what belongs to a guest customer to the account's customer it merges into, such as orders and transactions.
/// </summary>
public interface ICustomerMergeHandler
{
    Task MergingAsync(CustomerMergeContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// A guest customer merging into the customer of a user whose email is verified.
/// </summary>
public sealed class CustomerMergeContext
{
    public CustomerMergeContext(Models.Customer guest, Models.Customer into, string userId)
    {
        Guest = guest;
        Into = into;
        UserId = userId;
    }

    public Models.Customer Guest { get; }

    public Models.Customer Into { get; }

    /// <summary>
    /// Gets the user the account's customer belongs to.
    /// </summary>
    public string UserId { get; }
}
