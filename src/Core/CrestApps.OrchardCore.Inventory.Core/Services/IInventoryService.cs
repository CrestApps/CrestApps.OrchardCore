using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Changes stock. Every change is a movement in the ledger, made under a lock on the stock item so two changes never
/// overwrite each other.
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
}

/// <summary>
/// Who changed the stock: an administrator, or <see langword="null"/> for the store itself.
/// </summary>
public sealed record StockActor(string Id, string Name);

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

public enum StockChangeError
{
    None = 0,
    NotFound = 1,
    InvalidQuantity = 2,

    /// <summary>
    /// Someone else is changing the same stock item; try again.
    /// </summary>
    Busy = 3,
}
