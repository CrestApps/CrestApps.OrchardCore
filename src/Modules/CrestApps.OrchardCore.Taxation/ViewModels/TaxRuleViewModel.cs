using System;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// Represents the data used to create or edit a tax rule.
/// </summary>
public class TaxRuleViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the tax rule is being created.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the display name of the tax rule.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tax rule is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the order in which the tax rule is evaluated.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the tax type applied by the tax rule.
    /// </summary>
    public string TaxType { get; set; }

    /// <summary>
    /// Gets or sets the display name of the tax applied by the tax rule.
    /// </summary>
    public string TaxName { get; set; }

    /// <summary>
    /// Gets or sets the code of the tax applied by the tax rule.
    /// </summary>
    public string TaxCode { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the jurisdiction where the tax rule applies.
    /// </summary>
    public string JurisdictionId { get; set; }

    /// <summary>
    /// Gets or sets the tax category code matched by the tax rule.
    /// </summary>
    public string CategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the customer tax type matched by the tax rule.
    /// </summary>
    public CustomerTaxType? CustomerType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tax is included in item prices.
    /// </summary>
    public bool IncludedInPrice { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tax compounds on prior taxes.
    /// </summary>
    public bool IsCompound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the recipient accounts for the tax (reverse charge).
    /// </summary>
    public bool ReverseCharge { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tax applies to shipping charges.
    /// </summary>
    public bool AppliesToShipping { get; set; }

    /// <summary>
    /// Gets or sets the minimum taxable amount required for the tax rule to apply.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the maximum taxable amount allowed for the tax rule to apply.
    /// </summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the tax rule becomes effective.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the tax rule stops being effective.
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }

    /// <summary>
    /// Gets or sets the available tax type options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TaxTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the available calculation method options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CustomerTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the available jurisdiction options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Jurisdictions { get; set; } = [];

    /// <summary>
    /// Gets or sets the available category options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Categories { get; set; } = [];
}
