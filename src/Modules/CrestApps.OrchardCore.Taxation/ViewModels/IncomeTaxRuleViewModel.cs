using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// The income tax rule editor.
/// </summary>
public class IncomeTaxRuleViewModel
{
    public string Name { get; set; }

    public string TaxCode { get; set; }

    public string Jurisdiction { get; set; }

    public string CollectedBy { get; set; }

    public string ExceptJurisdictions { get; set; }

    public bool ByPlaceOfWork { get; set; }

    public string Category { get; set; }

    public string[] IncomeKinds { get; set; } = [];

    public string Payer { get; set; }

    public string Method { get; set; }

    public string Rate { get; set; }

    public string FixedAmount { get; set; }

    public string TaxTableId { get; set; }

    public string WageBase { get; set; }

    public string YearlyExemption { get; set; }

    public string YearlyMaximum { get; set; }

    public string ShareOfTaxCode { get; set; }

    public string ShareMultiplier { get; set; }

    public bool Annualize { get; set; }

    public bool UsesElections { get; set; }

    public string AllowanceAmount { get; set; }

    public string SupplementalRate { get; set; }

    public string SupplementalExcessRate { get; set; }

    public string SupplementalExcessThreshold { get; set; }

    public string WageThreshold { get; set; }

    public Dictionary<string, string> WithholdingTables { get; set; } = [];

    public Dictionary<string, string> WithholdingParameters { get; set; } = [];

    [BindNever]
    public IList<IncomeTaxWithholdingMethodViewModel> WithholdingMethods { get; set; } = [];

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public bool Enabled { get; set; }

    [BindNever]
    public IList<SelectListItem> Categories { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Methods { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Tables { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> AvailableIncomeKinds { get; set; } = [];
}

/// <summary>
/// The tables and amounts of a withholding method, in the income tax rule editor.
/// </summary>
public class IncomeTaxWithholdingMethodViewModel
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public IList<(string Key, string Label)> Tables { get; set; } = [];

    public IList<(string Key, string Label)> Parameters { get; set; } = [];
}
