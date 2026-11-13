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

    /// <summary>
    /// Gets or sets the department keys on the register's keypad, in the order they're shown. None shows a key for
    /// every department that's on.
    /// </summary>
    public List<RegisterKey> Keys { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }
}

/// <summary>
/// A key on a register's keypad that rings up an amount in a department.
/// </summary>
public sealed class RegisterKey
{
    public string DepartmentId { get; set; }

    /// <summary>
    /// Gets or sets what the key reads, or <see langword="null"/> for the department's name.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the amount the key rings up when nothing is keyed in, such as 1.00 for a coffee, or
    /// <see langword="null"/> for a key that needs an amount keyed in first.
    /// </summary>
    public decimal? PresetAmount { get; set; }
}

/// <summary>
/// A department of the store a register rings up amounts in, such as Candy or Grocery: a sale without an item to
/// track, counted toward its department and taxed by its tax category.
/// </summary>
public sealed class PosDepartment : CatalogItem
{
    public long Id { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the department's number, such as 1 for Grocery, which receipts and reports show and which order
    /// lines keep as their SKU.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the code of the tax category its sales are taxed by, or <see langword="null"/> for the store's
    /// default rate.
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the color of its key on the register, a CSS color such as #2e7d32, or <see langword="null"/>.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the least amount one ring-up can be, or <see langword="null"/> for any amount.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the most one ring-up can be, so a mistyped amount is caught, or <see langword="null"/> for no limit.
    /// </summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>
    /// Gets or sets where it comes among the departments: a smaller number first.
    /// </summary>
    public int Position { get; set; }

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

    /// <summary>
    /// Gets or sets the tax collected on the sales.
    /// </summary>
    public decimal Tax { get; set; }

    /// <summary>
    /// Gets or sets what was rung up in each department, before tax, largest first.
    /// </summary>
    public List<RegisterShiftDepartmentTotal> Departments { get; set; } = [];
}

/// <summary>
/// What a shift rang up in one department.
/// </summary>
public sealed class RegisterShiftDepartmentTotal
{
    public string DepartmentId { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets how many were rung up: a line of three counts three.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets what they came to before tax.
    /// </summary>
    public decimal Total { get; set; }
}
