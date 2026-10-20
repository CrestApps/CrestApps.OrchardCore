using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.PointOfSale.Services;

namespace CrestApps.OrchardCore.PointOfSale.Api;

/// <summary>
/// A register the caller may sell at.
/// </summary>
/// <param name="LocationId">The inventory location its sales take stock from.</param>
/// <param name="Address">The store's address, which its sales are taxed by.</param>
/// <param name="OpenShiftId">Its open shift, when there is one.</param>
public sealed record ApiRegister(string Id, string Name, string LocationId, string Currency, ApiAddress Address, string OpenShiftId);

/// <summary>
/// A shift at a register: what it's sold and the cash in its drawer.
/// </summary>
/// <param name="Status"><c>open</c> or <c>closed</c>.</param>
/// <param name="Cash">The cash kept for sales: what was handed over less the change.</param>
/// <param name="Other">What gift cards and other balances paid.</param>
/// <param name="ExpectedCash">What the drawer should hold: the float, the cash kept, and the cash put in less taken out.</param>
/// <param name="CountedCash">What was counted in the drawer at the close.</param>
/// <param name="Difference">The counted cash less the expected; negative when the drawer is short.</param>
public sealed record ApiShift(
    string Id,
    string RegisterId,
    string RegisterName,
    string Status,
    ApiMoney OpeningFloat,
    int Sales,
    ApiMoney Total,
    ApiMoney Cash,
    ApiMoney Card,
    ApiMoney Other,
    ApiMoney Refunded,
    IReadOnlyList<ApiCashMovement> CashMovements,
    ApiMoney ExpectedCash,
    ApiMoney CountedCash,
    ApiMoney Difference,
    string OpenedBy,
    DateTime OpenedUtc,
    string ClosedBy,
    DateTime? ClosedUtc);

/// <summary>
/// Cash put in the drawer or taken out of it outside a sale.
/// </summary>
public sealed record ApiCashMovement(ApiMoney Amount, string Reason, string By, DateTime Utc);

/// <summary>
/// A sale rung up: its order and its checkout, which takes codes and gift cards before it's paid.
/// </summary>
public sealed record ApiPosSale(string OrderId, string OrderNumber, ApiCheckout Checkout);

/// <summary>
/// What paying a sale came to.
/// </summary>
/// <param name="Status"><c>completed</c>: paid and placed; or <c>due</c>: <c>amountDue</c> is still to pay.</param>
/// <param name="Change">The change to hand back.</param>
/// <param name="Receipt">The sale's receipt, once it's completed.</param>
public sealed record ApiPosPayment(string Status, ApiMoney AmountDue, ApiMoney Change, ApiPosReceipt Receipt);

/// <summary>
/// A sale's receipt, for a till app to print in its own layout.
/// </summary>
public sealed record ApiPosReceipt(
    string OrderId,
    string OrderNumber,
    DateTime Utc,
    string Staff,
    IReadOnlyList<ApiPosReceiptLine> Lines,
    ApiMoney Subtotal,
    ApiMoney Discount,
    ApiMoney Tax,
    ApiMoney Total,
    IReadOnlyList<ApiPosReceiptPayment> Payments,
    ApiMoney Change);

public sealed record ApiPosReceiptLine(string Description, int Quantity, ApiMoney UnitPrice, ApiMoney Total);

/// <summary>
/// How part of a sale was paid: <c>Cash</c>, <c>Card</c>, a gift card.
/// </summary>
public sealed record ApiPosReceiptPayment(string Method, ApiMoney Amount);

/// <summary>
/// What a return at the till came to.
/// </summary>
/// <param name="CashBack">The cash handed back from the drawer.</param>
public sealed record ApiPosReturn(string OrderId, string OrderNumber, ApiMoney Refunded, ApiMoney CashBack, IReadOnlyList<string> Messages);

public sealed class ApiOpenShiftRequest
{
    /// <summary>
    /// The cash put in the drawer at the start.
    /// </summary>
    public decimal? OpeningFloat { get; set; }
}

public sealed class ApiCashMovementRequest
{
    /// <summary>
    /// Positive for cash put in, negative for cash taken out.
    /// </summary>
    public decimal? Amount { get; set; }

    public string Reason { get; set; }
}

public sealed class ApiCloseShiftRequest
{
    public decimal? CountedCash { get; set; }

    public string Note { get; set; }
}

public sealed class ApiPosSaleRequest
{
    public string RegisterId { get; set; }

    public List<PosSaleLine> Lines { get; set; } = [];

    /// <summary>
    /// The customer buying, when staff look them up: their points and receipt follow.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// A walk-in buyer's email, for their receipt, when they aren't a customer yet.
    /// </summary>
    public string CustomerEmail { get; set; }

    public string CustomerName { get; set; }
}

public sealed class ApiPosPayRequest
{
    /// <summary>
    /// The cash handed over; more than what's due is the change.
    /// </summary>
    public decimal? Cash { get; set; }

    /// <summary>
    /// A card taken on the counter terminal for what's left after the cash.
    /// </summary>
    public ApiPosCard Card { get; set; }
}

public sealed class ApiPosCard
{
    /// <summary>
    /// The terminal's approval code, kept with the payment.
    /// </summary>
    public string ApprovalCode { get; set; }
}

public sealed class ApiEmailReceiptRequest
{
    public string Email { get; set; }
}

public sealed class ApiPosReturnRequest
{
    public string OrderId { get; set; }

    /// <summary>
    /// The register the return is made at: cash given back comes out of its drawer, and stock goes back at its store.
    /// </summary>
    public string RegisterId { get; set; }

    /// <summary>
    /// How much to refund; all that's refundable when not given.
    /// </summary>
    public decimal? Amount { get; set; }

    public string Reason { get; set; }

    /// <summary>
    /// Whether the items go back on the shelf; they do unless told otherwise.
    /// </summary>
    public bool? Restock { get; set; }
}
