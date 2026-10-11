using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Resolves a <see cref="CustomerOwner"/> into an <see cref="ICustomerContact"/> so a consumer can address
/// the buyer for statements and notifications without depending on the authenticated user service. An
/// authenticated owner resolves through the user store; a guest owner resolves from the contact snapshot
/// the consumer captured at purchase time.
/// </summary>
public interface ICustomerContactResolver
{
    /// <summary>
    /// Resolves the contact for the supplied owner, or <see langword="null"/> when no contact can be
    /// determined (for example an authenticated owner whose user no longer exists, or a guest with no
    /// captured contact).
    /// </summary>
    /// <param name="owner">The owner to resolve.</param>
    /// <param name="guestContact">
    /// The contact snapshot captured for a guest owner at purchase time. It is ignored for authenticated
    /// owners and may be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<ICustomerContact> ResolveAsync(CustomerOwner owner, ICustomerContact guestContact, CancellationToken cancellationToken = default);
}
