using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// Represents the calculation-method specific fields of a tax rule. The fields shown depend on the rule
/// source, so a method only renders the inputs it actually consumes.
/// </summary>
public class TaxRuleMethodViewModel
{
    /// <summary>
    /// Gets or sets the percentage rate used by rate-based tax calculations.
    /// </summary>
    public decimal? Rate { get; set; }

    /// <summary>
    /// Gets or sets the amount used by fixed-amount tax calculations.
    /// </summary>
    public decimal? FixedAmount { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the tax table used by table-based tax calculations.
    /// </summary>
    public string TaxTableId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rate field is shown for the current method.
    /// </summary>
    [BindNever]
    public bool ShowRate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the fixed amount field is shown for the current method.
    /// </summary>
    [BindNever]
    public bool ShowFixedAmount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tax table field is shown for the current method.
    /// </summary>
    [BindNever]
    public bool ShowTaxTable { get; set; }

    /// <summary>
    /// Gets or sets the available tax table options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TaxTables { get; set; } = [];
}
