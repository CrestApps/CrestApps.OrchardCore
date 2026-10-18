using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// What a customer bought: the cart's lines frozen at the prices they checked out at.
/// </summary>
/// <remarks>
/// An order holds lines, not money. The payment, its tax and any refund belong to the checkout session the order
/// names; the order is marked paid only when that checkout completes.
/// </remarks>
public sealed class Order : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number shown to the customer and on the admin screens, such as <c>261013-7F3K2Q</c>.
    /// </summary>
    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets the user who placed the order, or <see langword="null"/> for a guest.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the cart the order was made from.
    /// </summary>
    public string CartId { get; set; }

    public string Currency { get; set; }

    public List<OrderLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the total of the lines, before discounts and tax.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Gets or sets what was charged, with discounts and tax, once the order is paid.
    /// </summary>
    public decimal? Total { get; set; }

    /// <summary>
    /// Gets or sets the tax charged, once the order is paid.
    /// </summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets what the discounts took off, such as a coupon, as the checkout applied them.
    /// </summary>
    public List<OrderDiscount> Discounts { get; set; } = [];

    /// <summary>
    /// Gets the total of the discounts.
    /// </summary>
    public decimal DiscountTotal
        => Discounts?.Sum(discount => discount.Amount) ?? 0m;

    public OrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the checkout session that pays for the order.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the customer who placed the order, when the Customers feature is enabled.
    /// </summary>
    public string CustomerId { get; set; }

    public string CustomerName { get; set; }

    public string CustomerEmail { get; set; }

    public string CustomerPhone { get; set; }

    /// <summary>
    /// Gets or sets the address the order was billed to, copied when it was paid for. Changing or deleting the
    /// address it came from never changes it.
    /// </summary>
    public Address BillingAddress { get; set; }

    /// <summary>
    /// Gets or sets where the order is shipped, or <see langword="null"/> when nothing in it ships.
    /// </summary>
    public Address ShippingAddress { get; set; }

    /// <summary>
    /// Gets or sets how the order is shipped and what it cost, or <see langword="null"/> when nothing in it ships.
    /// </summary>
    public OrderShipping Shipping { get; set; }

    /// <summary>
    /// Gets or sets how much of the order has been shipped, or <see langword="null"/> when nothing in it ships or
    /// nothing records shipments.
    /// </summary>
    public OrderFulfillmentStatus? FulfillmentStatus { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public DateTime? PaidUtc { get; set; }

    /// <summary>
    /// Gets or sets when the order confirmation was emailed to the customer, so it is sent once.
    /// </summary>
    public DateTime? ConfirmationSentUtc { get; set; }

    /// <summary>
    /// Gets or sets when the order's personal details were removed, at the customer's request or after the retention
    /// window.
    /// </summary>
    public DateTime? AnonymizedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the order was canceled without being paid.
    /// </summary>
    public DateTime? CanceledUtc { get; set; }

    /// <summary>
    /// Gets or sets why the order was canceled.
    /// </summary>
    public OrderCancelReason? CancelReason { get; set; }

    /// <summary>
    /// Gets or sets why the store canceled the order, in its own words, when it was canceled from its page.
    /// </summary>
    public string CancelNote { get; set; }

    /// <summary>
    /// Gets or sets the notes on the order, oldest first: what the store wrote, and what happened, such as a refund.
    /// </summary>
    public List<OrderNote> Notes { get; set; } = [];

    /// <summary>
    /// Gets or sets the money given back for the order, one entry per refund that went through.
    /// </summary>
    public List<OrderRefund> Refunds { get; set; } = [];

    /// <summary>
    /// Gets how much was given back.
    /// </summary>
    public decimal RefundedAmount
        => Refunds?.Sum(refund => refund.Amount) ?? 0m;

    /// <summary>
    /// Gets whether money was given back: none, part of what was paid, or all of it.
    /// </summary>
    public OrderRefundStatus RefundStatus
        => RefundedAmount <= 0m
            ? OrderRefundStatus.None
            : Total.HasValue && RefundedAmount >= Total.Value ? OrderRefundStatus.Refunded : OrderRefundStatus.PartiallyRefunded;

    /// <summary>
    /// Removes who the order was for: the name, email and phone, and the billing address but its country and region,
    /// which tax records need. The lines, totals and dates stay for the store's accounts.
    /// </summary>
    public void Anonymize(DateTime now)
    {
        CustomerName = null;
        CustomerEmail = null;
        CustomerPhone = null;
        BillingAddress = BillingAddress is null ? null : new Address { Country = BillingAddress.Country, Region = BillingAddress.Region };
        ShippingAddress = ShippingAddress is null ? null : new Address { Country = ShippingAddress.Country, Region = ShippingAddress.Region };
        AnonymizedUtc ??= now;
        ModifiedUtc = now;
    }

    /// <summary>
    /// Gets the number of units ordered.
    /// </summary>
    public int ItemCount
        => Lines?.Sum(line => line.Quantity) ?? 0;
}

/// <summary>
/// One product of an order, as it was when the order was placed.
/// </summary>
public sealed class OrderLine
{
    public string LineId { get; set; }

    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the version of the product that was bought.
    /// </summary>
    public string ContentItemVersionId { get; set; }

    public string ContentType { get; set; }

    public string Title { get; set; }

    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the product's price the line was bought at, or <see langword="null"/> for its product part's price.
    /// </summary>
    public string PriceId { get; set; }

    public string PriceName { get; set; }

    /// <summary>
    /// Gets or sets the variant bought, for a product sold in variants.
    /// </summary>
    public string VariantId { get; set; }

    /// <summary>
    /// Gets or sets the variant's values when it was bought, such as "Red / M".
    /// </summary>
    public string VariantTitle { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public ProductType ProductType { get; set; }

    public string TaxCategoryCode { get; set; }

    public string TaxClassificationCode { get; set; }

    /// <summary>
    /// Gets or sets the categories the product was in when it was ordered (taxonomy term ids), for discounts limited
    /// to some categories.
    /// </summary>
    public string[] CategoryIds { get; set; } = [];
}

/// <summary>
/// Where an order is in its life.
/// </summary>
/// <summary>
/// A note on an order.
/// </summary>
public sealed class OrderNote
{
    public string Id { get; set; }

    public string Text { get; set; }

    public OrderNoteKind Kind { get; set; }

    /// <summary>
    /// Gets or sets who wrote it or did what it records, or <see langword="null"/> for the site itself.
    /// </summary>
    public string AuthorName { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// What an <see cref="OrderNote"/> is.
/// </summary>
public enum OrderNoteKind
{
    /// <summary>
    /// Something the store wrote.
    /// </summary>
    Note = 0,

    /// <summary>
    /// Something that happened to the order, such as a refund or a cancellation.
    /// </summary>
    Event = 1,
}

/// <summary>
/// Money given back for an order.
/// </summary>
public sealed class OrderRefund
{
    /// <summary>
    /// Gets or sets the identifier of the payment refund, so the same refund is never counted twice.
    /// </summary>
    public string RefundId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }

    public string Reason { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Whether money was given back for an order.
/// </summary>
public enum OrderRefundStatus
{
    None = 0,

    PartiallyRefunded = 1,

    Refunded = 2,
}

/// <summary>
/// A discount an order was given, such as a coupon.
/// </summary>
public sealed class OrderDiscount
{
    /// <summary>
    /// Gets or sets the code the buyer entered, or <see langword="null"/> for a discount without one.
    /// </summary>
    public string Code { get; set; }

    public string Description { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the key of the discount provider that gave it, such as <c>Coupons</c>.
    /// </summary>
    public string ProviderKey { get; set; }
}

/// <summary>
/// The shipping method an order was shipped by, and what it cost before tax.
/// </summary>
public sealed class OrderShipping
{
    public string MethodId { get; set; }

    public string RateId { get; set; }

    public string ZoneId { get; set; }

    public string ProviderName { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public decimal Amount { get; set; }
}

public enum OrderFulfillmentStatus
{
    Unfulfilled = 0,

    /// <summary>
    /// Some of the goods have left, in one or more shipments.
    /// </summary>
    PartiallyFulfilled = 1,

    Fulfilled = 2,
}

public enum OrderStatus
{
    /// <summary>
    /// Made from a cart, and not yet sent to the checkout.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// The checkout for it has started and has not been paid.
    /// </summary>
    AwaitingPayment = 1,

    /// <summary>
    /// Its checkout completed.
    /// </summary>
    Paid = 2,

    /// <summary>
    /// Replaced by a newer order from the same cart before it was paid.
    /// </summary>
    Canceled = 3,
}
