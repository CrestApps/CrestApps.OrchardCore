using CrestApps.OrchardCore.PointOfSale.Models;

namespace CrestApps.OrchardCore.PointOfSale.ViewModels;

/// <summary>
/// The registers a staff member may pick to sell at in the browser.
/// </summary>
public class WebRegisterIndexViewModel
{
    public IReadOnlyList<WebRegisterChoice> Registers { get; set; } = [];
}

public sealed record WebRegisterChoice(Register Register, RegisterShift OpenShift);

/// <summary>
/// The web register page: the register, its keys and where its requests go.
/// </summary>
public class WebRegisterViewModel
{
    public Register Register { get; set; }

    public string Currency { get; set; }

    public IReadOnlyList<WebRegisterKey> Keys { get; set; } = [];

    /// <summary>
    /// Gets or sets whether sales can be paid on the store's own card terminal.
    /// </summary>
    public bool CanTakeCards { get; set; }
}

/// <summary>
/// A department key as the web register draws it.
/// </summary>
public sealed record WebRegisterKey(string DepartmentId, string Label, string Color, decimal? PresetAmount, decimal? MinimumAmount, decimal? MaximumAmount);

public sealed class WebRegisterOpenShiftRequest
{
    public decimal? OpeningFloat { get; set; }
}

public sealed class WebRegisterCashRequest
{
    public decimal? Amount { get; set; }

    public string Reason { get; set; }
}

public sealed class WebRegisterCloseShiftRequest
{
    public decimal? CountedCash { get; set; }

    public string Note { get; set; }
}

public sealed class WebRegisterSaleRequest
{
    public List<WebRegisterSaleLine> Lines { get; set; } = [];
}

/// <summary>
/// A line of the ticket: an amount in a department, or a product found by its SKU.
/// </summary>
public sealed class WebRegisterSaleLine
{
    public string DepartmentId { get; set; }

    public decimal? Amount { get; set; }

    public string ProductId { get; set; }

    public string VariantId { get; set; }

    public int Quantity { get; set; } = 1;
}

public sealed class WebRegisterPayRequest
{
    public string CheckoutId { get; set; }

    /// <summary>
    /// Gets or sets the cash handed over; more than what's due is the change.
    /// </summary>
    public decimal? Cash { get; set; }

    /// <summary>
    /// Gets or sets whether what's left is paid on the store's own card terminal.
    /// </summary>
    public bool Card { get; set; }

    /// <summary>
    /// Gets or sets the card terminal's approval code, kept with the payment.
    /// </summary>
    public string ApprovalCode { get; set; }
}

public sealed class WebRegisterCancelRequest
{
    public string CheckoutId { get; set; }
}

/// <summary>
/// A plain till receipt, when the store's branded receipts aren't on.
/// </summary>
public class WebRegisterReceiptViewModel
{
    public Register Register { get; set; }

    public CrestApps.OrchardCore.Orders.Core.Models.Order Order { get; set; }

    /// <summary>
    /// Gets or sets when it was sold, as the store reads it.
    /// </summary>
    public DateTime SoldAt { get; set; }
}
