using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// The fields every income tax rule has, whatever its source: what the tax is, where it applies, who pays and when.
/// </summary>
public class IncomeTaxRuleViewModel
{
    public string Name { get; set; }

    public string TaxCode { get; set; }

    public string Jurisdiction { get; set; }

    public string CollectedBy { get; set; }

    public string ExceptJurisdictions { get; set; }

    public bool ByPlaceOfWork { get; set; }

    public string Residency { get; set; }

    public string ExclusiveGroup { get; set; }

    public int GroupPriority { get; set; }

    public string ElectionsFrom { get; set; }

    public string Category { get; set; }

    public string[] IncomeKinds { get; set; } = [];

    public string Payer { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public bool Enabled { get; set; }

    [BindNever]
    public string SourceDisplayName { get; set; }

    [BindNever]
    public string SourceDescription { get; set; }

    [BindNever]
    public IList<string> DroppedOnSave { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Categories { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> AvailableIncomeKinds { get; set; } = [];
}

/// <summary>
/// A flat rate rule: a rate on the income within a wage base, above a threshold, after an exemption, up to a maximum.
/// </summary>
public class IncomeTaxFlatRateViewModel
{
    public string Rate { get; set; }

    public bool RateFromRegistration { get; set; }

    public string WageBase { get; set; }

    public string WageThreshold { get; set; }

    public string YearlyExemption { get; set; }

    public string YearlyMaximum { get; set; }

    public string ExemptionAllowance { get; set; }
}

/// <summary>
/// A rule that is a multiple of another tax of the same pay.
/// </summary>
public class IncomeTaxShareOfTaxViewModel
{
    public string ShareOfTaxCode { get; set; }

    public string ShareMultiplier { get; set; }

    public string YearlyMaximum { get; set; }
}

/// <summary>
/// A rule worked out on a tax table or progressive brackets.
/// </summary>
public class IncomeTaxTableViewModel
{
    public string Method { get; set; }

    public string TaxTableId { get; set; }

    public string WageBase { get; set; }

    public bool Annualize { get; set; }

    public bool UsesElections { get; set; }

    public string AllowanceAmount { get; set; }

    public string SupplementalRate { get; set; }

    public string SupplementalExcessRate { get; set; }

    public string SupplementalExcessThreshold { get; set; }

    [BindNever]
    public IList<SelectListItem> Methods { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Tables { get; set; } = [];
}

/// <summary>
/// A rule of the same amount each pay.
/// </summary>
public class IncomeTaxFixedAmountViewModel
{
    public string FixedAmount { get; set; }

    public string YearlyMaximum { get; set; }
}

/// <summary>
/// A rule worked out by a country pack's withholding method: the method's tables and amounts, and how the person's
/// election and supplemental income are treated.
/// </summary>
public class IncomeTaxWithholdingViewModel
{
    public Dictionary<string, string> Tables { get; set; } = [];

    public Dictionary<string, string> Parameters { get; set; } = [];

    public bool UsesElections { get; set; }

    public string AllowanceAmount { get; set; }

    public string SupplementalRate { get; set; }

    public string SupplementalExcessRate { get; set; }

    public string SupplementalExcessThreshold { get; set; }

    public string DeductedTaxCodes { get; set; }

    [BindNever]
    public bool ShowDeductedTaxCodes { get; set; }

    [BindNever]
    public string MethodDisplayName { get; set; }

    [BindNever]
    public bool HandlesSupplementalIncome { get; set; }

    [BindNever]
    public IList<(string Key, string Label, bool Required)> TableInputs { get; set; } = [];

    [BindNever]
    public IList<(string Key, string Label, bool Required)> ParameterInputs { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> AvailableTables { get; set; } = [];
}
