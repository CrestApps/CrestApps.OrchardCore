using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Orders.ViewModels;

public class OrdersIndexViewModel
{
    public OrdersIndexOptions Options { get; set; } = new();

    public IReadOnlyList<Order> Orders { get; set; } = [];

    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets whether the list is the signed-in customer's own orders.
    /// </summary>
    public bool IsOwnOrders { get; set; }

    [BindNever]
    public IShape Pager { get; set; }

    /// <summary>
    /// Gets or sets the orders, pager and region shapes the <c>admin-list</c> tag renders, in the
    /// layout (List or Grid) the site or the user picked.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

public class OrdersIndexOptions
{
    public string Search { get; set; }

    public OrderStatusFilter Status { get; set; }

    [BindNever]
    public List<SelectListItem> Statuses { get; set; } = [];
}

public enum OrderStatusFilter
{
    /// <summary>
    /// Paid orders and orders whose checkout started.
    /// </summary>
    Placed,

    Paid,

    AwaitingPayment,

    Canceled,

    All,

    /// <summary>
    /// Orders not checked out yet, such as the store's drafts.
    /// </summary>
    Drafts,
}

public class OrderDetailViewModel
{
    public Order Order { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer manages orders, rather than looking at one of their own.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets what other modules add to the order's page, such as its shipments.
    /// </summary>
    public IShape Sections { get; set; }

    /// <summary>
    /// Gets or sets what can still be refunded and what is still owed, for a viewer who manages orders.
    /// </summary>
    public CrestApps.OrchardCore.Orders.Services.OrderMoney Money { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the site can email the confirmation again.
    /// </summary>
    public bool CanResendConfirmation { get; set; }

    /// <summary>
    /// Gets or sets the link the buyer pays an order the store made for them from, or <see langword="null"/>.
    /// </summary>
    public string PayUrl { get; set; }
}

public class OrderRefundInputModel
{
    public decimal? Amount { get; set; }

    public string Reason { get; set; }

    public bool Restock { get; set; }

    /// <summary>
    /// Gets or sets whether the refund is given to the buyer as store credit, rather than back to how they paid.
    /// </summary>
    public bool AsStoreCredit { get; set; }
}

public class OrderCancelInputModel
{
    public string Note { get; set; }

    public bool Refund { get; set; }

    public bool Restock { get; set; }
}

/// <summary>
/// The products a confirmation page offers next: what goes with what was just bought.
/// </summary>
public class OrderConfirmationOffersViewModel
{
    public IList<global::OrchardCore.ContentManagement.ContentItem> Products { get; set; } = [];
}

public class OrderConfirmationViewModel
{
    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets whether the order belongs to a signed-in customer, who can find it again under My Orders.
    /// </summary>
    public bool IsSignedIn { get; set; }

    /// <summary>
    /// Gets or sets the signed link a guest opens their order with.
    /// </summary>
    public string GuestOrderUrl { get; set; }

    /// <summary>
    /// Gets or sets the link to create an account, for a guest on a site that lets visitors register.
    /// </summary>
    public string RegisterUrl { get; set; }

    /// <summary>
    /// Gets or sets the token that lets a guest make an account from this page and take the order into it, or
    /// <see langword="null"/> when they can't (not a guest, no email, or the site doesn't let visitors register).
    /// </summary>
    public string AccountToken { get; set; }

    /// <summary>
    /// Gets or sets the email the account would get: the order's.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the page to come back to after making the account fails.
    /// </summary>
    public string ReturnUrl { get; set; }
}

public class OrderCreateAccountViewModel
{
    public string Token { get; set; }

    public string Password { get; set; }

    public string ConfirmPassword { get; set; }
}

/// <summary>
/// The order bump on the payment step.
/// </summary>
public class OrderBumpViewModel
{
    public bool Accept { get; set; }

    [BindNever]
    public string Headline { get; set; }

    [BindNever]
    public string Title { get; set; }

    [BindNever]
    public string Price { get; set; }

    /// <summary>
    /// Gets or sets the product's own price, shown struck through when the offer is cheaper.
    /// </summary>
    [BindNever]
    public string ListPrice { get; set; }
}

public class OrderSettingsViewModel
{
    public bool SendConfirmations { get; set; }

    public bool SendRecoveryEmails { get; set; }

    public int RecoveryDelayHours { get; set; } = 1;

    public string OrderBumpProductId { get; set; }

    public decimal? OrderBumpPercentOff { get; set; }

    public string OrderBumpHeadline { get; set; }

    [BindNever]
    public string ProductSearchEndpoint { get; set; }

    [BindNever]
    public string InitialOrderBumpJson { get; set; }

    /// <summary>
    /// Gets or sets whether the site has the base URL recovery emails link back through.
    /// </summary>
    [BindNever]
    public bool HasBaseUrl { get; set; }
}

public class OrderViewViewModel
{
    /// <summary>
    /// Gets or sets the order's printable invoice, for the buyer.
    /// </summary>
    public string InvoiceUrl { get; set; }

    public Order Order { get; set; }

    /// <summary>
    /// Gets or sets what other modules add to the order's page, such as its shipments and their tracking links.
    /// </summary>
    public IShape Sections { get; set; }

    /// <summary>
    /// Gets or sets the link to pay what is still owed on the order, or <see langword="null"/>.
    /// </summary>
    public string PayUrl { get; set; }

    public string RegisterUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the order is shown in the signed-in customer's account, beside its menu.
    /// </summary>
    public bool InAccount { get; set; }
}

public class AccountOrdersViewModel
{
    public IReadOnlyList<Order> Orders { get; set; } = [];

    /// <summary>
    /// Gets or sets the orders still owed, such as ones paid by bank transfer before the money arrives.
    /// </summary>
    public ISet<string> OwedOrderIds { get; set; } = new HashSet<string>();

    public IShape Pager { get; set; }
}

public class FindOrderViewModel
{
    public string Email { get; set; }
}

/// <summary>
/// Track your order: an order's number and the email it was placed with open its page. The model of the
/// <c>OrderTrack</c> shape a theme can override.
/// </summary>
public class TrackOrderViewModel
{
    public string OrderNumber { get; set; }

    public string Email { get; set; }

    [BindNever]
    public bool NotFound { get; set; }
}

/// <summary>
/// A printable document of an order: its invoice or a packing slip. Its shape (<c>OrderInvoice</c> or
/// <c>OrderPackingSlip</c>) is what a theme overrides to change it.
/// </summary>
public class OrderDocumentViewModel
{
    public Order Order { get; set; }

    public OrderDocumentHeader Header { get; set; } = new();

    public string Title { get; set; }

    public DateTime Placed { get; set; }

    public DateTime? Paid { get; set; }

    public IList<string> BilledTo { get; set; } = [];

    public IList<string> ShipsTo { get; set; } = [];

    public IList<OrderDocumentLine> Lines { get; set; } = [];

    public IList<(string Label, decimal Amount)> Discounts { get; set; } = [];

    /// <summary>
    /// Gets or sets what's still owed, for an invoice.
    /// </summary>
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Gets or sets what the invoice says of the order: paid, payment due, refunded or canceled.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the shipment a packing slip is for, or <see langword="null"/> for the whole order.
    /// </summary>
    public string ShipmentNumber { get; set; }
}

public class OrderDocumentLine
{
    public string Description { get; set; }

    public string Sku { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// The store as its documents name it: from the receipt settings, else the site's name.
/// </summary>
public class OrderDocumentHeader
{
    public string Name { get; set; }

    public string LogoUrl { get; set; }

    public string Address { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    public string Website { get; set; }

    public string Footer { get; set; }
}

/// <summary>
/// The page around a printable order document: where it goes back to, and whether it's the store's or the buyer's.
/// </summary>
public class OrderDocumentPageViewModel
{
    public Order Order { get; set; }

    public IShape Document { get; set; }

    public bool IsAdmin { get; set; }

    public string BackUrl { get; set; }

    public string PrintLabel { get; set; }
}

public class DraftOrderEditViewModel
{
    public string CustomerEmail { get; set; }

    public string CustomerName { get; set; }

    public string Note { get; set; }

    public DateTime? DueDate { get; set; }

    public string AddsToOrderId { get; set; }

    public List<DraftOrderLineViewModel> Lines { get; set; } = [];

    public string[] AddProductIds { get; set; } = [];

    [BindNever]
    public Order Order { get; set; }

    [BindNever]
    public string AddsToOrderNumber { get; set; }

    [BindNever]
    public IReadOnlyList<string> Problems { get; set; } = [];

    [BindNever]
    public decimal Subtotal { get; set; }

    [BindNever]
    public string Currency { get; set; }

    [BindNever]
    public string ProductSearchEndpoint { get; set; }

    [BindNever]
    public bool CanEmail { get; set; }
}

public class DraftOrderLineViewModel
{
    public string LineId { get; set; }

    public string ContentItemId { get; set; }

    public string VariantId { get; set; }

    public int Quantity { get; set; }

    public bool Remove { get; set; }

    [BindNever]
    public string Title { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Variants { get; set; } = [];

    [BindNever]
    public decimal? LineTotal { get; set; }

    [BindNever]
    public string Problem { get; set; }
}
