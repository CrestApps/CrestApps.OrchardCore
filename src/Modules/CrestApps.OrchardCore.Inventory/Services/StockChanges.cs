using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// What Commerce › Inventory and the inventory AI tools share around a stock change: committing it so a checkout that
/// changed the same stock at the same moment cannot be overwritten, and the units an item holds for unpaid orders.
/// </summary>
internal static class StockChanges
{
    /// <summary>
    /// Commits a stock change made in the session's unit of work. When another change to the same stock item was
    /// committed first, the store refuses this one; nothing of it is kept and the result says the stock was busy.
    /// </summary>
    /// <param name="session">The session the change was made in.</param>
    /// <param name="result">The change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The change, or a <see cref="StockChangeError.Busy"/> failure.</returns>
    public static async Task<StockChangeResult> CommitAsync(ISession session, StockChangeResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Succeeded)
        {
            return result;
        }

        try
        {
            await session.SaveChangesAsync(cancellationToken);

            return result;
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();

            return StockChangeResult.Fail(StockChangeError.Busy);
        }
    }

    /// <summary>
    /// Lists the units of a stock item held for unpaid orders, the soonest to expire first.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="stockItemId">The stock item.</param>
    /// <param name="take">The most holds to list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The holds.</returns>
    public static async Task<StockReservation[]> ListHoldsAsync(ISession session, string stockItemId, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var holds = await session.Query<StockReservation, StockReservationIndex>(index =>
                index.StockItemId == stockItemId && index.Status == StockReservationStatus.Held,
                collection: InventoryConstants.CollectionName)
            .OrderBy(index => index.ExpiresUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return holds.ToArray();
    }
}
