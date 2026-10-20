using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Marketing.Models;

/// <summary>
/// What a customer has bought: their paid orders, what those came to in the store's currency, and when. Kept up to date
/// as orders are paid and refunded, and built from earlier orders when the feature is turned on. Its id is the
/// customer's.
/// </summary>
public sealed class CustomerPurchases : CatalogItem
{
    // A customer's counted orders, so an order is never counted twice; beyond this, the oldest are forgotten.
    public const int MaxOrderIds = 5000;

    /// <summary>
    /// Gets or sets the document's id, which campaigns send in the order of.
    /// </summary>
    public long Id { get; set; }

    public string Email { get; set; }

    public int OrderCount { get; set; }

    /// <summary>
    /// Gets or sets what the paid orders came to, less refunds, in the store's currency.
    /// </summary>
    public decimal TotalSpent { get; set; }

    public DateTime? FirstOrderUtc { get; set; }

    public DateTime? LastOrderUtc { get; set; }

    /// <summary>
    /// Gets or sets the country of the last order's billing or shipping address.
    /// </summary>
    public string Country { get; set; }

    public List<string> OrderIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the refunds taken off <see cref="TotalSpent"/>, so a refund is never taken off twice.
    /// </summary>
    public List<string> RefundIds { get; set; } = [];

    public DateTime ModifiedUtc { get; set; }
}

/// <summary>
/// A group of customers by what they've bought, such as lapsed buyers: those who bought before, but not in 90 days.
/// </summary>
public sealed class Segment : CatalogItem
{
    public string Name { get; set; }

    public string Description { get; set; }

    public SegmentRules Rules { get; set; } = new();

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// What a segment's customers have in common. Every rule given has to hold; none given is every customer who bought.
/// </summary>
public sealed class SegmentRules
{
    public int? MinOrders { get; set; }

    public int? MaxOrders { get; set; }

    /// <summary>
    /// Gets or sets the least they've spent, in the store's currency.
    /// </summary>
    public decimal? MinSpent { get; set; }

    public decimal? MaxSpent { get; set; }

    /// <summary>
    /// Gets or sets the days since their last order at least, such as 90 for lapsed buyers.
    /// </summary>
    public int? LastOrderAtLeastDaysAgo { get; set; }

    /// <summary>
    /// Gets or sets the days their last order was within, such as 30 for recent buyers.
    /// </summary>
    public int? LastOrderWithinDays { get; set; }

    /// <summary>
    /// Gets or sets the days their first order was within, such as 30 for new customers.
    /// </summary>
    public int? FirstOrderWithinDays { get; set; }

    /// <summary>
    /// Gets or sets the two-letter country of their last order, or none for any.
    /// </summary>
    public string Country { get; set; }
}

/// <summary>
/// An email to a segment's customers who accept marketing: its subject and body (HTML with Liquid), and where its
/// sending is.
/// </summary>
public sealed class Campaign : CatalogItem
{
    public string Name { get; set; }

    public string SegmentId { get; set; }

    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the body, as HTML with Liquid: <c>{{ Customer.FirstName }}</c>, <c>{{ UnsubscribeUrl }}</c> and so on.
    /// </summary>
    public string Body { get; set; }

    public CampaignStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it starts sending, or none for as soon as it's sent.
    /// </summary>
    public DateTime? ScheduledUtc { get; set; }

    public DateTime? StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the document id of the last customer it went to, so sending picks up there.
    /// </summary>
    public string Cursor { get; set; }

    public int Sent { get; set; }

    /// <summary>
    /// Gets or sets the customers in the segment it didn't go to: no email, or they don't accept marketing.
    /// </summary>
    public int Skipped { get; set; }

    public int Failed { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// How far building customers' purchases from the orders paid before the feature was turned on has got.
/// </summary>
public sealed class PurchasesBackfill : CatalogItem
{
    public const string Id = "purchases-backfill";

    /// <summary>
    /// Gets or sets how many paid orders, newest first, have been read.
    /// </summary>
    public int Offset { get; set; }

    public DateTime? CompletedUtc { get; set; }
}

public enum CampaignStatus
{
    Draft,

    /// <summary>
    /// Sent, waiting for its time to start.
    /// </summary>
    Scheduled,

    Sending,

    Sent,

    Canceled,
}
