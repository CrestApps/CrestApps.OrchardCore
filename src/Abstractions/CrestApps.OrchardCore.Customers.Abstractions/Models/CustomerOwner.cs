namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// An immutable, provider-neutral identity of the buyer that owns a commerce record. It pairs the
/// <see cref="CustomerOwnerKind"/> with the stable owner id (a user id for authenticated buyers, or a
/// tenant-scoped guest customer id for guests) so ownership can be persisted and compared without a
/// consumer having to know whether the buyer has an account.
/// </summary>
public sealed class CustomerOwner : IEquatable<CustomerOwner>
{
    private CustomerOwner(CustomerOwnerKind kind, string id)
    {
        Kind = kind;
        Id = id;
    }

    /// <summary>
    /// Gets the kind of owner (authenticated user or guest).
    /// </summary>
    public CustomerOwnerKind Kind { get; }

    /// <summary>
    /// Gets the stable owner id: the user's unique id when <see cref="Kind"/> is
    /// <see cref="CustomerOwnerKind.Authenticated"/>, otherwise the guest customer id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Creates an owner for an authenticated user.
    /// </summary>
    /// <param name="userId">The authenticated user's unique id.</param>
    public static CustomerOwner ForUser(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return new CustomerOwner(CustomerOwnerKind.Authenticated, userId);
    }

    /// <summary>
    /// Creates an owner for an anonymous guest.
    /// </summary>
    /// <param name="guestCustomerId">The stable, tenant-scoped guest customer id.</param>
    public static CustomerOwner ForGuest(string guestCustomerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(guestCustomerId);

        return new CustomerOwner(CustomerOwnerKind.Guest, guestCustomerId);
    }

    /// <inheritdoc/>
    public bool Equals(CustomerOwner other)
    {
        if (other is null)
        {
            return false;
        }

        return Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
        => Equals(obj as CustomerOwner);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(Kind, Id);
}
