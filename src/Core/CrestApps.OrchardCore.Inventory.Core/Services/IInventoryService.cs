using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Changes stock. Every change to the units on hand is a movement in the ledger. Changes are made in the caller's unit
/// of work and committed with it: stock items are concurrency-checked, so when two changes to one item race, the
/// second commit fails with a <see cref="YesSql.ConcurrencyException"/> and nothing of it is kept; the caller says
/// "try again" or retries.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Records units received (a positive quantity) or a correction (<see cref="StockMovementKind.Adjusted"/>, either
    /// sign).
    /// </summary>
    Task<StockChangeResult> AdjustAsync(string stockItemId, StockMovementKind kind, int quantity, string reason, StockActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a count: the units on hand become <paramref name="count"/>, and the movement holds the difference.
    /// </summary>
    Task<StockChangeResult> SetCountAsync(string stockItemId, int count, string reason, StockActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes the units on hand from the movements, for when the cached count is in doubt.
    /// </summary>
    Task<StockChangeResult> RebuildAsync(string stockItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Holds the units an order needs, all of them or none: a counted product short of units, and not allowing
    /// backorders, refuses the whole order. Products that aren't counted are skipped.
    /// </summary>
    Task<StockReservationResult> ReserveAsync(string orderId, IEnumerable<StockRequest> lines, DateTime expiresUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sells what an order holds once it is paid. Lines with no hold left, because it expired or the order was
    /// replaced, are sold anyway: the order is paid. Selling an order again changes nothing.
    /// </summary>
    Task<StockReservationResult> CommitAsync(string orderId, IEnumerable<StockRequest> lines, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets go of what an order still holds. Releasing again changes nothing.
    /// </summary>
    Task<StockReservationResult> ReleaseAsync(string orderId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts what an order sold back into stock, such as when it was refunded or canceled, recording a Returned movement
    /// for each item. Putting an order back again changes nothing.
    /// </summary>
    /// <returns>How many units went back.</returns>
    Task<int> RestockAsync(string orderId, string reason, StockActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets go of holds past their expiry, and returns how many.
    /// </summary>
    Task<int> ReleaseExpiredAsync(int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// Who changed the stock: an administrator, or <see langword="null"/> for the store itself.
/// </summary>
public sealed record StockActor(string Id, string Name);

/// <summary>
/// Units of a product, or one of its variants, that an order needs.
/// </summary>
public sealed record StockRequest(string ContentItemId, string VariantId, int Quantity);

public sealed class StockChangeResult
{
    public bool Succeeded { get; init; }

    public StockItem Item { get; init; }

    public StockMovement Movement { get; init; }

    public StockChangeError Error { get; init; }

    public static StockChangeResult Success(StockItem item, StockMovement movement = null)
        => new() { Succeeded = true, Item = item, Movement = movement };

    public static StockChangeResult Fail(StockChangeError error)
        => new() { Error = error };
}

public sealed class StockReservationResult
{
    public bool Succeeded
        => Error == StockChangeError.None && Shortages.Count == 0;

    public StockChangeError Error { get; init; }

    /// <summary>
    /// Gets the counted products that didn't have enough units, with how many were available.
    /// </summary>
    public IReadOnlyList<StockShortage> Shortages { get; init; } = [];
}

public sealed record StockShortage(StockItem Item, int Requested, int Available);

public enum StockChangeError
{
    None = 0,
    NotFound = 1,
    InvalidQuantity = 2,

    /// <summary>
    /// Someone else was changing the same stock item; try again.
    /// </summary>
    Busy = 3,
}
