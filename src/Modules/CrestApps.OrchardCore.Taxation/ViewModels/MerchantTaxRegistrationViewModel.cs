using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// The fields of a tax registration in its editor.
/// </summary>
public class MerchantTaxRegistrationViewModel
{
    /// <summary>
    /// Gets or sets the registration's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction the business is registered in.
    /// </summary>
    public string JurisdictionId { get; set; }

    /// <summary>
    /// Gets or sets the tax type the registration covers, or empty for every type.
    /// </summary>
    public string TaxType { get; set; }

    /// <summary>
    /// Gets or sets the number the tax authority gave.
    /// </summary>
    public string RegistrationNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the business collects the tax under it.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the sales above which the business must collect the tax.
    /// </summary>
    public decimal? ThresholdAmount { get; set; }

    /// <summary>
    /// Gets or sets the first day the registration is effective.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day the registration is effective.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Gets or sets the jurisdictions to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Jurisdictions { get; set; } = [];

    /// <summary>
    /// Gets or sets the tax types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TaxTypes { get; set; } = [];
}

/// <summary>
/// The filing schedule of a tax registration in its editor.
/// </summary>
public class MerchantTaxRegistrationFilingViewModel
{
    /// <summary>
    /// Gets or sets the filing frequency, or empty when the registration files no return here.
    /// </summary>
    public string Frequency { get; set; }

    /// <summary>
    /// Gets or sets how many months after the period's last month a return is due.
    /// </summary>
    public int DueMonthsAfterPeriod { get; set; }

    /// <summary>
    /// Gets or sets the day of that month a return is due.
    /// </summary>
    public int DueDay { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a due date on a weekend moves to the Monday after.
    /// </summary>
    public bool MoveToNextBusinessDay { get; set; }

    /// <summary>
    /// Gets or sets the form of the periodic returns.
    /// </summary>
    public string Form { get; set; }

    /// <summary>
    /// Gets or sets the form of the yearly reconciliation return, or empty for none.
    /// </summary>
    public string AnnualForm { get; set; }

    /// <summary>
    /// Gets or sets the month after the year the annual return is due in.
    /// </summary>
    public int AnnualDueMonthsAfterYear { get; set; }

    /// <summary>
    /// Gets or sets the day of that month the annual return is due.
    /// </summary>
    public int AnnualDueDay { get; set; }

    /// <summary>
    /// Gets or sets the frequencies to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Frequencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the forms to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Forms { get; set; } = [];
}

/// <summary>
/// A row of the tax registrations.
/// </summary>
public class MerchantTaxRegistrationRowViewModel
{
    /// <summary>
    /// Gets or sets the registration.
    /// </summary>
    public MerchantTaxRegistration Registration { get; set; }

    /// <summary>
    /// Gets or sets the name of its jurisdiction.
    /// </summary>
    public string JurisdictionName { get; set; }
}
