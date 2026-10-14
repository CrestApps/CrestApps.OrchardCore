using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Products.ViewModels;

/// <summary>
/// The editable set of prices a product is offered at.
/// </summary>
public class ProductPricePartViewModel
{
    /// <summary>
    /// Gets or sets the prices being edited.
    /// </summary>
    public IList<ProductPriceViewModel> Prices { get; set; } = [];

    /// <summary>
    /// Gets or sets the currencies a price may be sold in.
    /// </summary>
    public IEnumerable<SelectListItem> Currencies { get; set; }

    /// <summary>
    /// Gets or sets the billing intervals a recurring price may use.
    /// </summary>
    public IEnumerable<SelectListItem> Intervals { get; set; }

    /// <summary>
    /// Gets or sets the kinds a price may be.
    /// </summary>
    public IEnumerable<SelectListItem> Kinds { get; set; }
}

/// <summary>
/// One editable price.
/// </summary>
public class ProductPriceViewModel
{
    /// <summary>
    /// Gets or sets the stable identifier. Empty for a row the editor has just added.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets the name shown to the buyer.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets whether the amount recurs.
    /// </summary>
    public PriceKind Kind { get; set; }

    /// <summary>
    /// Gets or sets how many interval units make one cycle.
    /// </summary>
    public int? BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets the billing interval.
    /// </summary>
    public BillingInterval? Interval { get; set; }

    /// <summary>
    /// Gets or sets how many cycles are billed before the agreement ends.
    /// </summary>
    public int? BillingCycleLimit { get; set; }

    /// <summary>
    /// Gets or sets how many days pass before billing starts.
    /// </summary>
    public int? StartDayDelay { get; set; }

    /// <summary>
    /// Gets or sets how many free days the buyer gets first.
    /// </summary>
    public int? TrialDays { get; set; }

    /// <summary>
    /// Gets or sets a one-time amount charged with the first cycle.
    /// </summary>
    public decimal? SetupFee { get; set; }

    /// <summary>
    /// Gets or sets what the setup fee is called.
    /// </summary>
    public string SetupFeeDescription { get; set; }

    /// <summary>
    /// Gets or sets whether the buyer names the amount.
    /// </summary>
    public bool AllowCustomAmount { get; set; }

    /// <summary>
    /// Gets or sets the least the buyer may name.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the most the buyer may name.
    /// </summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>
    /// Gets or sets whether the buyer may take more than one.
    /// </summary>
    public bool AllowQuantity { get; set; }

    /// <summary>
    /// Gets or sets the most the buyer may take.
    /// </summary>
    public int? MaximumQuantity { get; set; }

    /// <summary>
    /// Gets or sets whether this is the price offered by default.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets whether the price may be bought.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets when the price starts being offered.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets when the price stops being offered.
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the editor should drop this row.
    /// </summary>
    public bool Remove { get; set; }
}
