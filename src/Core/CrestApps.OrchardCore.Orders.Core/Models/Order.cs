using System.Text.Json.Nodes;
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
    /// Gets or sets what balances the buyer held, such as a gift card or store credit, paid toward
    /// <see cref="Total"/>. The rest was charged by the payment provider.
    /// </summary>
    public List<OrderTender> Tenders { get; set; } = [];

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
    /// Gets or sets where the order was made: <see cref="OrderChannels.Online"/>, at a store's register
    /// (<see cref="OrderChannels.PointOfSale"/>), or by staff (<see cref="OrderChannels.Draft"/>). Orders made before
    /// channels were recorded have none, and count as online.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the inventory location the order is sold from, such as the store whose register rang it up: its
    /// stock is held and sold there.
    /// </summary>
    public string LocationId { get; set; }

    /// <summary>
    /// Gets or sets the register a point-of-sale order was rung up on.
    /// </summary>
    public string RegisterId { get; set; }

    /// <summary>
    /// Gets or sets the register shift a point-of-sale order was rung up in.
    /// </summary>
    public string ShiftId { get; set; }

    /// <summary>
    /// Gets or sets the staff member who made the order, at a register or as a draft.
    /// </summary>
    public string StaffUserId { get; set; }

    public string StaffName { get; set; }

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
    /// Gets or sets what the buyer entered in the store's own checkout fields, as the content of a Checkout details
    /// item, or <see langword="null"/> when the store asks for none.
    /// </summary>
    public JsonObject CheckoutDetails { get; set; }

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
    /// Gets or sets when the buyer was emailed a link back to this order's checkout after leaving it unpaid, or
    /// <see langword="null"/> when they weren't.
    /// </summary>
    public DateTime? RecoveryEmailSentUtc { get; set; }

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
    /// Gets or sets the disputes, or chargebacks, the payer opened against the order's payment, oldest first.
    /// </summary>
    public List<OrderDispute> Disputes { get; set; } = [];

    /// <summary>
    /// Gets or sets how risky the gateway's fraud checks rated the order's payment, or <see langword="null"/> when it
    /// doesn't rate payments.
    /// </summary>
    public OrderPaymentRisk PaymentRisk { get; set; }

    /// <summary>
    /// Gets or sets the invoice of an order the store made for the buyer to pay, or <see langword="null"/> for one the
    /// buyer made from their cart.
    /// </summary>
    public OrderInvoice Invoice { get; set; }

    /// <summary>
    /// Gets whether a dispute against the order's payment is still open.
    /// </summary>
    public bool HasOpenDispute
        => Disputes?.Any(dispute => !dispute.IsClosed) == true;

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
    /// Removes who the order was for: the name, email and phone, what was entered in the store's checkout fields, and the
    /// billing address but its country and region, which tax records need. The lines, totals and dates stay for the
    /// store's accounts.
    /// </summary>
    public void Anonymize(DateTime now)
    {
        CustomerName = null;
        CustomerEmail = null;
        CustomerPhone = null;
        BillingAddress = BillingAddress is null ? null : new Address { Country = BillingAddress.Country, Region = BillingAddress.Region };
        ShippingAddress = ShippingAddress is null ? null : new Address { Country = ShippingAddress.Country, Region = ShippingAddress.Region };
        CheckoutDetails = null;
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

    /// <summary>
    /// Gets or sets the add-ons the buyer chose for the line, such as an engraving, which <see cref="UnitPrice"/>
    /// includes.
    /// </summary>
    public List<OrderLineAddOn> AddOns { get; set; } = [];

    /// <summary>
    /// Gets or sets the products a bundle line is made of, per bundle, as the bundle was when the order was placed.
    /// Stock is held and sold from them, not from the bundle.
    /// </summary>
    public List<OrderLineBundleItem> BundleItems { get; set; } = [];

    /// <summary>
    /// Gets or sets when a pre-ordered line ships, as it was when the order was placed, or <see langword="null"/>.
    /// </summary>
    public DateTime? ShipsFromUtc { get; set; }
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
/// An order the store put together for the buyer, such as from a phone call, and sent them to pay.
/// </summary>
public sealed class OrderInvoice
{
    /// <summary>
    /// Gets or sets who in the store made it.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets what the buyer reads with it.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets when it should be paid by, or <see langword="null"/>.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was last emailed to the buyer, or <see langword="null"/> while it's being put together.
    /// </summary>
    public DateTime? SentUtc { get; set; }

    /// <summary>
    /// Gets or sets the order this one adds to, such as items added after that one was paid.
    /// </summary>
    public string AddsToOrderId { get; set; }

    public string AddsToOrderNumber { get; set; }
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

    /// <summary>
    /// Gets or sets what the discount took something off: the goods bought now, the first billing cycle, or shipping.
    /// </summary>
    public Checkout.Models.DiscountTarget Target { get; set; }
}

/// <summary>
/// Part of an order's total paid from a balance the buyer held, such as a gift card or store credit.
/// </summary>
public sealed class OrderTender
{
    public string ProviderKey { get; set; }

    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets what the buyer saw, such as "Gift card ending 4F2K".
    /// </summary>
    public string Description { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// The shipping method an order was shipped by, and what it cost before tax.
/// </summary>
public sealed class OrderShipping
{
    /// <summary>
    /// The provider name of an order the buyer picks up from one of the store's locations, rather than having it
    /// shipped. Its shipping address is then the location's.
    /// </summary>
    public const string PickupProviderName = "Pickup";

    public string MethodId { get; set; }

    public string RateId { get; set; }

    public string ZoneId { get; set; }

    public string ProviderName { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the location the order is fulfilled from, such as the shop it's picked up at, or
    /// <see langword="null"/> to let inventory choose.
    /// </summary>
    public string LocationId { get; set; }

    /// <summary>
    /// Gets whether the buyer picks the order up rather than having it shipped.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPickup
        => string.Equals(ProviderName, PickupProviderName, StringComparison.Ordinal);
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

/// <summary>
/// An add-on chosen for an order line, as it read when the order was placed.
/// </summary>
public sealed class OrderLineAddOn
{
    public string AddOnId { get; set; }

    /// <summary>
    /// Gets or sets what the add-on was called, such as "Engraving".
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets what the buyer typed, or <see langword="null"/> for a ticked box.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the extra price per unit it cost.
    /// </summary>
    public decimal Price { get; set; }
}

/// <summary>
/// A product in a bundle line, and how many of it each bundle holds.
/// </summary>
public sealed class OrderLineBundleItem
{
    public string ContentItemId { get; set; }

    public string Title { get; set; }

    public int Quantity { get; set; }
}
