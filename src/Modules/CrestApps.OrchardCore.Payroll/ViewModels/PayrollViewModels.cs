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

/// <summary>
/// The pay profile list.
/// </summary>
public class PayProfilesIndexViewModel
{
    public string EmployerId { get; set; }

    [BindNever]
    public IList<SelectListItem> Employers { get; set; } = [];

    [BindNever]
    public IReadOnlyList<PayProfileRow> Profiles { get; set; } = [];

    [BindNever]
    public bool CanAdd { get; set; }

    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// A row of the pay profile list.
/// </summary>
public class PayProfileRow
{
    public EmployeePayProfile Profile { get; set; }

    public string StaffName { get; set; }

    public string EmployerName { get; set; }

    public string ScheduleName { get; set; }

    public IReadOnlyList<CrestApps.OrchardCore.Payroll.Services.PayBlocker> Blockers { get; set; } = [];
}

/// <summary>
/// The pay profile editor's main card.
/// </summary>
public class PayProfileViewModel
{
    public string StaffMemberId { get; set; }

    public string EmployerId { get; set; }

    public string PayScheduleId { get; set; }

    public string WorkerType { get; set; }

    public string OvertimeStatus { get; set; }

    public string Status { get; set; }

    public string LegalName { get; set; }

    public string WorkSiteTeamId { get; set; }

    public DateOnly? RetroPayFrom { get; set; }

    public string DepartmentTeamId { get; set; }

    public string ActivityCode { get; set; }

    public string Job { get; set; }

    public string TaxId { get; set; }

    public bool RemoveTaxId { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public string StaffName { get; set; }

    [BindNever]
    public string TaxIdHint { get; set; }

    [BindNever]
    public bool CanRevealTaxId { get; set; }

    [BindNever]
    public string ProfileId { get; set; }

    [BindNever]
    public IList<string> SameTaxIdStaff { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> StaffMembers { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Employers { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Schedules { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Teams { get; set; } = [];

    [BindNever]
    public IReadOnlyList<CrestApps.OrchardCore.Payroll.Services.PayBlocker> Blockers { get; set; } = [];
}

/// <summary>
/// The pay profile's elections card: the history and a new election.
/// </summary>
public class PayProfileElectionsViewModel
{
    public string NewJurisdiction { get; set; }

    public DateOnly? NewEffectiveFrom { get; set; }

    public Dictionary<string, string> NewValues { get; set; } = new(StringComparer.Ordinal);

    [BindNever]
    public IList<TaxElection> History { get; set; } = [];

    [BindNever]
    public IList<string> Jurisdictions { get; set; } = [];

    [BindNever]
    public IList<CrestApps.OrchardCore.Payroll.Services.ElectionField> Fields { get; set; } = [];

    [BindNever]
    public IDictionary<string, string> FieldLabels { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// The pay profile's net pay card.
/// </summary>
public class PayProfileDistributionViewModel
{
    public IList<NetPayRowViewModel> Rows { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> BankAccounts { get; set; } = [];
}

/// <summary>
/// A row of the net pay split, as typed.
/// </summary>
public class NetPayRowViewModel
{
    public string Method { get; set; }

    public string BankAccountId { get; set; }

    public string Split { get; set; }

    public string Value { get; set; }
}

/// <summary>
/// The pay profile's home address card.
/// </summary>
public class PayProfileAddressViewModel
{
    public AddressEditorViewModel Address { get; set; } = new();

    [BindNever]
    public IShape AddressEditor { get; set; }
}

/// <summary>
/// The pay item list of a profile.
/// </summary>
public class PayItemsIndexViewModel
{
    [BindNever]
    public EmployeePayProfile Profile { get; set; }

    [BindNever]
    public string StaffName { get; set; }

    [BindNever]
    public string EmployerName { get; set; }

    [BindNever]
    public IReadOnlyList<EmployeePayItem> Items { get; set; } = [];

    [BindNever]
    public IReadOnlyDictionary<string, PayCode> Codes { get; set; } = new Dictionary<string, PayCode>();

    [BindNever]
    public bool CanManage { get; set; }

    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// The pay item editor.
/// </summary>
public class PayItemViewModel
{
    public string PayCodeId { get; set; }

    public string Amount { get; set; }

    public string Percent { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string EmployerMatchPercent { get; set; }

    public string EmployerMatchLimitPercent { get; set; }

    public string Goal { get; set; }

    public string PeriodMaximum { get; set; }

    public string YearMaximum { get; set; }

    public string Note { get; set; }

    [BindNever]
    public string PaidTowardGoal { get; set; }

    [BindNever]
    public string ArrearsBalance { get; set; }

    [BindNever]
    public IList<SelectListItem> Codes { get; set; } = [];
}

/// <summary>
/// The Payroll card on a staff member's profile.
/// </summary>
public class StaffPayrollCardViewModel
{
    [BindNever]
    public string StaffMemberId { get; set; }

    [BindNever]
    public IReadOnlyList<PayProfileRow> Profiles { get; set; } = [];

    [BindNever]
    public bool CanAdd { get; set; }
}

/// <summary>
/// The page that shows a full tax id once.
/// </summary>
public class PayProfileTaxIdViewModel
{
    [BindNever]
    public string ProfileId { get; set; }

    [BindNever]
    public string StaffName { get; set; }

    [BindNever]
    public string TaxId { get; set; }
}

/// <summary>
/// The Payroll card of the pay code editor.
/// </summary>
public class PayrollPayCodeViewModel
{
    public string Calculation { get; set; }

    public string[] BaseCodeIds { get; set; } = [];

    public Dictionary<string, string> Treatments { get; set; } = new(StringComparer.Ordinal);

    public bool Supplemental { get; set; }

    public bool CountsInRegularRate { get; set; }

    public bool CountsAsHoursWorked { get; set; }

    public string PeriodLimit { get; set; }

    public IList<DatedAmountViewModel> YearLimits { get; set; } = [];

    public string LifetimeLimit { get; set; }

    public int Priority { get; set; }

    public string Arrears { get; set; }

    public string Proration { get; set; }

    public bool InRetroPay { get; set; }

    public string[] FormBoxes { get; set; } = [];

    [BindNever]
    public string ClassDefault { get; set; }

    [BindNever]
    public IList<SelectListItem> Categories { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> BaseCodes { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> FormBoxOptions { get; set; } = [];
}

/// <summary>
/// A row of dated amounts, such as a yearly limit.
/// </summary>
public class DatedAmountViewModel
{
    public DateOnly? EffectiveFrom { get; set; }

    public string Amount { get; set; }
}
