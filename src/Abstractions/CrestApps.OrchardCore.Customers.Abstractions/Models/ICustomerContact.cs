namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// A provider-neutral contact for a buyer. It carries only what a statement or notification needs to
/// address the buyer, so a guest (who has no user account) and an authenticated user are represented the
/// same way to the delivering code.
/// </summary>
public interface ICustomerContact
{
    /// <summary>
    /// Gets the display name used to greet the buyer, when known.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the email address used to reach the buyer, when known.
    /// </summary>
    string Email { get; }
}
