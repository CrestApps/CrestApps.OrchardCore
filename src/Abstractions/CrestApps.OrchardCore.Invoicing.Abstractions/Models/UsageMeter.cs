using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// Something a customer uses and is billed for by quantity, such as API calls, gigabytes stored or seats. Usage is
/// recorded against it (<see cref="UsageRecord"/>) and a recurring plan's usage charge bills a period's total.
/// </summary>
public sealed class UsageMeter : CatalogItem, INameAwareModel
{
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the key systems record usage with, such as <c>api-calls</c>.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the unit printed on invoices, such as <c>calls</c> or <c>GB</c>.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets how a period's records add up.
    /// </summary>
    public UsageAggregation Aggregation { get; set; }

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// How a meter's records add up over a period.
/// </summary>
public enum UsageAggregation
{
    /// <summary>
    /// The quantities added up, such as calls made.
    /// </summary>
    Sum = 0,

    /// <summary>
    /// The largest quantity, such as the most seats used at once.
    /// </summary>
    Maximum = 1,

    /// <summary>
    /// The latest quantity, such as the storage used at the end of the period.
    /// </summary>
    Last = 2,
}

/// <summary>
/// One reading of a meter for a customer, recorded once per event id.
/// </summary>
public sealed class UsageRecord : CatalogItem
{
    public string MeterId { get; set; }

    public PartyRef Party { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets when the usage happened, in UTC.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the caller's id of the event, which makes recording it again do nothing.
    /// </summary>
    public string EventId { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// A recurring plan's charge for a meter's usage in the period that just ended, priced by tiers.
/// </summary>
public sealed class RecurringUsageCharge
{
    public string MeterId { get; set; }

    /// <summary>
    /// Gets or sets the name printed on the invoice lines; the period can be written in as on any plan line.
    /// </summary>
    public string Name { get; set; }

    public string TaxCategoryCode { get; set; }

    public UsageTierMode TierMode { get; set; }

    /// <summary>
    /// Gets or sets the price tiers, lowest first; the last one has no upper bound.
    /// </summary>
    public IList<UsagePriceTier> Tiers { get; set; } = [];
}

/// <summary>
/// How tiers price a quantity.
/// </summary>
public enum UsageTierMode
{
    /// <summary>
    /// Each band of the quantity at its own tier's price: the first 1,000 at one price, the next at another.
    /// </summary>
    Graduated = 0,

    /// <summary>
    /// All of the quantity at the price of the tier it falls in.
    /// </summary>
    Volume = 1,
}

/// <summary>
/// One price tier: the price of each unit up to a quantity.
/// </summary>
public sealed class UsagePriceTier
{
    /// <summary>
    /// Gets or sets the quantity the tier goes up to, or <see langword="null"/> for no limit.
    /// </summary>
    public decimal? UpTo { get; set; }

    public decimal UnitPrice { get; set; }
}
