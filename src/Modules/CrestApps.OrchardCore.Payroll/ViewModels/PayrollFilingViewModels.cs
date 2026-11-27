using CrestApps.OrchardCore.Payroll.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Payroll.ViewModels;

/// <summary>
/// The liabilities page: the taxes owed by period and the deposits recorded.
/// </summary>
public class PayrollLiabilitiesViewModel
{
    public string EmployerId { get; set; }

    [BindNever]
    public IShape EmployerFilter { get; set; }

    public int Year { get; set; }

    public TaxDepositViewModel NewDeposit { get; set; } = new();

    [BindNever]
    public IList<SelectListItem> Employers { get; set; } = [];

    [BindNever]
    public IReadOnlyList<TaxLiabilityPeriod> Periods { get; set; } = [];

    [BindNever]
    public IReadOnlyList<TaxDeposit> Deposits { get; set; } = [];

    [BindNever]
    public AdminListContext PeriodList { get; set; }

    [BindNever]
    public AdminListContext DepositList { get; set; }
}

/// <summary>
/// A deposit typed on the liabilities page.
/// </summary>
public class TaxDepositViewModel
{
    public string EmployerId { get; set; }

    public string Jurisdiction { get; set; }

    public string Group { get; set; }

    public DateOnly? PeriodStart { get; set; }

    public DateOnly? PeriodEnd { get; set; }

    public string Amount { get; set; }

    public DateOnly? PaidOn { get; set; }

    public string Reference { get; set; }
}

/// <summary>
/// The filings page.
/// </summary>
public class PayrollFilingsIndexViewModel
{
    public string EmployerId { get; set; }

    [BindNever]
    public IShape EmployerFilter { get; set; }

    public int Year { get; set; }

    [BindNever]
    public IList<SelectListItem> Employers { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Forms { get; set; } = [];

    [BindNever]
    public IReadOnlyList<PayrollFiling> Filings { get; set; } = [];

    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// A filing's page.
/// </summary>
public class PayrollFilingDetailsViewModel
{
    [BindNever]
    public PayrollFiling Filing { get; set; }

    [BindNever]
    public string EmployerName { get; set; }

    [BindNever]
    public string FormName { get; set; }

    [BindNever]
    public DateOnly FiledOn { get; set; }

    [BindNever]
    public PayrollFiling Amends { get; set; }

    /// <summary>
    /// Gets or sets the label of each box of the form, by "person:" or "filing:" and its number.
    /// </summary>
    [BindNever]
    public IReadOnlyDictionary<string, string> BoxLabels { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// The year end page.
/// </summary>
public class PayrollYearEndViewModel
{
    public string EmployerId { get; set; }

    [BindNever]
    public IShape EmployerFilter { get; set; }

    public int Year { get; set; }

    [BindNever]
    public IList<SelectListItem> Employers { get; set; } = [];

    [BindNever]
    public bool Closed { get; set; }

    [BindNever]
    public IReadOnlyList<string> Blockers { get; set; } = [];
}

/// <summary>
/// The tax forms of a person in My Pay.
/// </summary>
public class MyPayTaxFormViewModel
{
    [BindNever]
    public PayrollFiling Filing { get; set; }

    [BindNever]
    public PayrollFilingPerson Person { get; set; }

    [BindNever]
    public string EmployerName { get; set; }

    /// <summary>
    /// Gets or sets what the person's statement of the form is called, such as Form W-2 or T4 slip.
    /// </summary>
    [BindNever]
    public string StatementName { get; set; }
}

/// <summary>
/// The journal settings card.
/// </summary>
public class PayrollJournalSettingsViewModel
{
    public string NetPayAccount { get; set; }

    public string WageExpenseAccount { get; set; }

    public string DeductionLiabilityAccount { get; set; }

    public string TaxLiabilityAccount { get; set; }

    public string EmployerTaxExpenseAccount { get; set; }

    public string ContributionExpenseAccount { get; set; }

    public string ContributionLiabilityAccount { get; set; }

    public string ReimbursementExpenseAccount { get; set; }

    public IList<PayrollJournalMapping> Mappings { get; set; } = [];
}

/// <summary>
/// A filing row.
/// </summary>
public class PayrollFilingRowViewModel
{
    public PayrollFiling Filing { get; set; }

    public string EmployerName { get; set; }
}
