using CrestApps.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Marketplace.Models;

/// <summary>
/// A seller on the marketplace: the people who run it, the commission the store keeps on its sales, and the Stripe
/// account it's paid out to.
/// </summary>
public sealed class Vendor : CatalogItem
{
    public string Name { get; set; }

    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the users who run the vendor and see its sales and payouts.
    /// </summary>
    public List<string> UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the usernames of <see cref="UserIds"/>, as they were when added.
    /// </summary>
    public List<string> UserNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the percent the store keeps of each sale, or <see langword="null"/> for the store's default.
    /// </summary>
    public decimal? CommissionPercent { get; set; }

    public VendorStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the country its Stripe account is made in, as two letters.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the Stripe Connect account it's paid out to.
    /// </summary>
    public string StripeAccountId { get; set; }

    /// <summary>
    /// Gets or sets whether its Stripe account can take transfers: onboarding done and transfers active.
    /// </summary>
    public bool PayoutsEnabled { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }
}

public enum VendorStatus
{
    Active = 0,

    /// <summary>
    /// Its sales are still recorded, but nothing is paid out.
    /// </summary>
    Suspended = 1,
}

/// <summary>
/// Names a product's vendor.
/// </summary>
public sealed class VendorPart : ContentPart
{
    public string VendorId { get; set; }
}

/// <summary>
/// One entry of a vendor's earnings: what a sale owes the vendor after the store's commission, or what a refund takes
/// back. Entries are held in escrow until released, then paid out.
/// </summary>
public sealed class VendorEarning : CatalogItem
{
    public string VendorId { get; set; }

    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    public string LineId { get; set; }

    public string Title { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets what the buyer paid for the line, after its share of the order's discounts; negative for a refund.
    /// </summary>
    public decimal Gross { get; set; }

    public decimal CommissionPercent { get; set; }

    public decimal Commission { get; set; }

    /// <summary>
    /// Gets or sets what the vendor gets: the gross less the commission.
    /// </summary>
    public decimal Net { get; set; }

    public VendorEarningKind Kind { get; set; }

    public VendorEarningStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the earliest it's released, after the store's hold.
    /// </summary>
    public DateTime HoldUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets whether it waits for its order to be fulfilled too, such as goods that have to arrive.
    /// </summary>
    public bool AwaitsFulfillment { get; set; }

    public DateTime? ReleasedUtc { get; set; }

    public string PayoutId { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public enum VendorEarningKind
{
    Sale = 0,
    Refund = 1,
}

public enum VendorEarningStatus
{
    /// <summary>
    /// In escrow: not released yet.
    /// </summary>
    Held = 0,

    /// <summary>
    /// Released, waiting to be paid out.
    /// </summary>
    Available = 1,

    PaidOut = 2,
}

/// <summary>
/// A transfer of a vendor's released earnings to its Stripe account.
/// </summary>
public sealed class VendorPayout : CatalogItem
{
    public string VendorId { get; set; }

    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }

    public List<string> EarningIds { get; set; } = [];

    public VendorPayoutStatus Status { get; set; }

    public string TransferId { get; set; }

    public string Failure { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public enum VendorPayoutStatus
{
    Paid = 0,
    Failed = 1,
}

/// <summary>
/// How the marketplace takes its commission and when it releases what vendors earn.
/// </summary>
public sealed class MarketplaceSettings
{
    /// <summary>
    /// Gets or sets the percent the store keeps of each vendor's sale, unless the vendor has its own.
    /// </summary>
    public decimal CommissionPercent { get; set; } = 10m;

    /// <summary>
    /// Gets or sets how many days a sale is held in escrow after it's paid, such as for returns.
    /// </summary>
    public int HoldDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets whether a sale of goods is also held until its order is fulfilled.
    /// </summary>
    public bool ReleaseOnFulfillment { get; set; } = true;

    /// <summary>
    /// Gets or sets whether released earnings are paid out by themselves, rather than when the store chooses.
    /// </summary>
    public bool AutomaticPayouts { get; set; }

    /// <summary>
    /// Gets or sets the country a vendor's Stripe account is made in when the vendor names none.
    /// </summary>
    public string DefaultCountry { get; set; } = "US";
}
