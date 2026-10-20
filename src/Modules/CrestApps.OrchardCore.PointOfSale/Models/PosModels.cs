using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.PointOfSale.Models;

/// <summary>
/// A till at one of the store's locations: where sales are rung up, and the cash drawer a shift counts.
/// </summary>
public sealed class Register : CatalogItem
{
    public long Id { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the inventory location the register sells from: its stock is what the register's sales take.
    /// </summary>
    public string LocationId { get; set; }

    /// <summary>
    /// Gets or sets the store's street address, which the register's sales are taxed by and its receipts show.
    /// </summary>
    public Address Address { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the roles whose staff may sell here; none lets anyone who may sell at a register.
    /// </summary>
    public List<string> Roles { get; set; } = [];

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }
}

/// <summary>
/// A stretch of selling at a register, from opening the drawer with a float to counting it at the close.
/// </summary>
public sealed class RegisterShift : CatalogItem
{
    public long Id { get; set; }

    public string RegisterId { get; set; }

    public string RegisterName { get; set; }

    public string LocationId { get; set; }

    public string Currency { get; set; }

    public RegisterShiftStatus Status { get; set; }

    public string OpenedByUserId { get; set; }

    public string OpenedByName { get; set; }

    public DateTime OpenedUtc { get; set; }

    /// <summary>
    /// Gets or sets the cash put in the drawer at the start.
    /// </summary>
    public decimal OpeningFloat { get; set; }

    public List<RegisterCashMovement> CashMovements { get; set; } = [];

    public string ClosedByUserId { get; set; }

    public string ClosedByName { get; set; }

    public DateTime? ClosedUtc { get; set; }

    /// <summary>
    /// Gets or sets the cash counted in the drawer at the close.
    /// </summary>
    public decimal? CountedCash { get; set; }

    /// <summary>
    /// Gets or sets the cash the drawer should have held at the close.
    /// </summary>
    public decimal? ExpectedCash { get; set; }

    public string CloseNote { get; set; }

    /// <summary>
    /// Gets or sets what the shift sold, kept when it closes.
    /// </summary>
    public RegisterShiftSummary Summary { get; set; }

    /// <summary>
    /// Returns the cash the drawer should hold: the float, the cash kept for sales (handed over less the change), and
    /// the cash put in less the cash taken out, refunds at the till included.
    /// </summary>
    public decimal GetExpectedCash(decimal cashKeptForSales)
        => OpeningFloat + cashKeptForSales + (CashMovements ?? []).Sum(movement => movement.Amount);
}

public enum RegisterShiftStatus
{
    Open = 0,
    Closed = 1,
}

/// <summary>
/// Cash put in the drawer (a positive amount) or taken out of it (a negative one) outside a sale, such as for change.
/// </summary>
public sealed class RegisterCashMovement
{
    public decimal Amount { get; set; }

    public string Reason { get; set; }

    public string ByUserId { get; set; }

    public string ByName { get; set; }

    public DateTime Utc { get; set; }
}

/// <summary>
/// What a shift sold, and how it was paid.
/// </summary>
public sealed class RegisterShiftSummary
{
    public int Sales { get; set; }

    public decimal Total { get; set; }

    /// <summary>
    /// Gets or sets the cash kept for sales: what was handed over less the change given.
    /// </summary>
    public decimal Cash { get; set; }

    public decimal Card { get; set; }

    /// <summary>
    /// Gets or sets what gift cards and other balances paid.
    /// </summary>
    public decimal Other { get; set; }

    public decimal Refunded { get; set; }
}
