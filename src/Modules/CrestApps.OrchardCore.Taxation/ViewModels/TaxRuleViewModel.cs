using System;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

public class TaxRuleViewModel
{
    public bool IsNew { get; set; }

    public string Name { get; set; }

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; }

    public string TaxType { get; set; }

    public string TaxName { get; set; }

    public string TaxCode { get; set; }

    public string JurisdictionId { get; set; }

    public string CategoryCode { get; set; }

    public CustomerTaxType? CustomerType { get; set; }

    public string CalculationMethod { get; set; }

    public decimal? Rate { get; set; }

    public decimal? FixedAmount { get; set; }

    public bool IncludedInPrice { get; set; }

    public bool IsCompound { get; set; }

    public bool AppliesToShipping { get; set; }

    public decimal? MinimumAmount { get; set; }

    public decimal? MaximumAmount { get; set; }

    public DateTime? EffectiveFromUtc { get; set; }

    public DateTime? EffectiveToUtc { get; set; }

    [BindNever]
    public IList<SelectListItem> TaxTypes { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> CalculationMethods { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> CustomerTypes { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Jurisdictions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Categories { get; set; } = [];
}
