using CrestApps.OrchardCore.Carts.Core.Indexes;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Carts.Core.Services;

/// <summary>
/// Stores carts in their own YesSql collection.
/// </summary>
public interface ICartStore
{
    /// <summary>
    /// Gets a cart by its identifier.
    /// </summary>
    ValueTask<Cart> FindByIdAsync(string cartId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cart of a signed-in user.
    /// </summary>
    Task<Cart> FindByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the guest cart whose token has the given hash.
    /// </summary>
    Task<Cart> FindByGuestTokenHashAsync(string guestTokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets carts that expired before the given time, oldest first.
    /// </summary>
    Task<IReadOnlyList<Cart>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);

    ValueTask CreateAsync(Cart cart, CancellationToken cancellationToken = default);

    ValueTask UpdateAsync(Cart cart, CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(Cart cart, CancellationToken cancellationToken = default);
}

public sealed class CartStore : DocumentCatalog<Cart, CartIndex>, ICartStore
{
    // Two tabs adding to the same cart at once must not overwrite each other's lines.
    protected override bool CheckConcurrency => true;

    public CartStore(ISession session)
        : base(session)
    {
        CollectionName = CartConstants.CollectionName;
    }

    public Task<Cart> FindByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult<Cart>(null);
        }

        return Session.Query<Cart, CartIndex>(index => index.UserId == userId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Cart> FindByGuestTokenHashAsync(string guestTokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(guestTokenHash))
        {
            return Task.FromResult<Cart>(null);
        }

        return Session.Query<Cart, CartIndex>(index => index.GuestTokenHash == guestTokenHash, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Cart>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
    {
        var carts = await Session.Query<Cart, CartIndex>(index => index.ExpiresUtc < nowUtc, collection: CollectionName)
            .OrderBy(index => index.ExpiresUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return carts.ToArray();
    }
}
