using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Payroll.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Payroll.ViewModels;

/// <summary>
/// The employer editor's details and registrations.
/// </summary>
public class EmployerViewModel
{
    public string Name { get; set; }

    public string LegalName { get; set; }

    public string TaxId { get; set; }

    public string CountryPack { get; set; }

    public string Currency { get; set; }

    public string LogoPath { get; set; }

    public string SignatoryName { get; set; }

    public string SignatoryTitle { get; set; }

    public string ContactName { get; set; }

    public string ContactEmail { get; set; }

    public string ContactPhone { get; set; }

    public bool Active { get; set; } = true;

    public IList<EmployerRegistrationRow> Registrations { get; set; } = [];
}

/// <summary>
/// A row of the registrations: one registration and one of its dated rates.
/// </summary>
public class EmployerRegistrationRow
{
    public string Jurisdiction { get; set; }

    public string Agency { get; set; }

    public string AccountId { get; set; }

    public string DepositFrequency { get; set; }

    public string Rate { get; set; }

    public DateOnly? RateFrom { get; set; }
}

/// <summary>
/// The employer's address card.
/// </summary>
public class EmployerAddressViewModel
{
    public AddressEditorViewModel Address { get; set; } = new();

    [BindNever]
    public IShape AddressEditor { get; set; }
}

/// <summary>
/// The employer's funding account card, with Banking on.
/// </summary>
public class EmployerFundingViewModel
{
    public string FundingBankAccountId { get; set; }

    [BindNever]
    public IList<SelectListItem> Accounts { get; set; } = [];
}

/// <summary>
/// Payroll's card on a team.
/// </summary>
public class PayrollTeamViewModel
{
    public string LedgerCode { get; set; }

    public string Jurisdictions { get; set; }
}

/// <summary>
/// Payroll's settings.
/// </summary>
public class PayrollSettingsViewModel
{
    public bool AllowPreparerToApprove { get; set; }

    public bool AllowChangingOwnPay { get; set; }

    public string DefaultCountryPack { get; set; }

    public string DefaultCurrency { get; set; }
}

/// <summary>
/// The employer list: the employers the user may manage, and whether they may add one.
/// </summary>
public class EmployersIndexViewModel
{
    [BindNever]
    public IReadOnlyList<Employer> Employers { get; set; } = [];

    [BindNever]
    public bool CanAdd { get; set; }

    [BindNever]
    public AdminListContext List { get; set; }
}
