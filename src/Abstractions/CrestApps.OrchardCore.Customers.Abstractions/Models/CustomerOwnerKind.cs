namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// Identifies whether a buyer is an authenticated user or an anonymous guest. This lets a reusable
/// commerce record (for example an outstanding transaction or a future order) express ownership without
/// assuming every buyer has a user account.
/// </summary>
public enum CustomerOwnerKind
{
    /// <summary>
    /// The buyer is an authenticated Orchard Core user. The owner id is the user's unique id. This is the
    /// default so records written before guest ownership existed keep their authenticated meaning.
    /// </summary>
    Authenticated = 0,

    /// <summary>
    /// The buyer is an anonymous guest. The owner id is a stable, tenant-scoped guest customer id rather
    /// than a user id, so a guest obligation is never orphaned under a null owner.
    /// </summary>
    Guest = 1,
}
