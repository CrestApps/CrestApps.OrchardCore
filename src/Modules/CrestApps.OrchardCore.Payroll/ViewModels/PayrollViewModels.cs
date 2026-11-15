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

    public bool EmployeeReview { get; set; }

    public string DefaultCountryPack { get; set; }

    public string DefaultCurrency { get; set; }

    public string TaxProvider { get; set; }

    public string MinimumNetPay { get; set; }

    public string MinimumHourlyWage { get; set; }

    public string MaximumTipCredit { get; set; }

    [BindNever]
    public IList<SelectListItem> TaxProviders { get; set; } = [];
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

    public bool Tipped { get; set; }

    public bool BackupWithholding { get; set; }

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

/// <summary>
/// The payroll tax rule editor.
/// </summary>
public class PayrollTaxRuleViewModel
{
    public string Name { get; set; }

    public string TaxCode { get; set; }

    public string Jurisdiction { get; set; }

    public string Category { get; set; }

    public string Payer { get; set; }

    public string Method { get; set; }

    public string Rate { get; set; }

    public string FixedAmount { get; set; }

    public string TaxTableId { get; set; }

    public string WageBase { get; set; }

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
    public IList<PayrollWithholdingMethodViewModel> WithholdingMethods { get; set; } = [];

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public bool Enabled { get; set; }

    [BindNever]
    public IList<SelectListItem> Categories { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Methods { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Tables { get; set; } = [];
}

/// <summary>
/// The tables and amounts of a withholding method, in the tax rule editor.
/// </summary>
public class PayrollWithholdingMethodViewModel
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public IList<(string Key, string Label)> Tables { get; set; } = [];

    public IList<(string Key, string Label)> Parameters { get; set; } = [];
}

/// <summary>
/// What a pay profile would be paid for the current period.
/// </summary>
public class PayPreviewViewModel
{
    public string ProfileId { get; set; }

    public string StaffName { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public DateOnly PayDate { get; set; }

    public string Currency { get; set; }

    public IList<PayPreviewLine> Lines { get; set; } = [];

    public decimal Gross { get; set; }

    public decimal Net { get; set; }

    public IList<PayStatementPayment> Payments { get; set; } = [];

    public List<string> Warnings { get; set; } = [];

    public bool MissingSchedule { get; set; }
}

/// <summary>
/// A line of a pay preview.
/// </summary>
/// <param name="Line">The line.</param>
/// <param name="Code">The pay code's code, or <see langword="null"/> for a tax.</param>
public sealed record PayPreviewLine(PayStatementLine Line, string Code);

/// <summary>
/// The pay run list.
/// </summary>
public class PayRunsIndexViewModel
{
    public string EmployerId { get; set; }

    [BindNever]
    public IList<SelectListItem> Employers { get; set; } = [];

    [BindNever]
    public bool CanAdd { get; set; }

    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// The editor of a new pay run.
/// </summary>
public class PayRunViewModel
{
    public string EmployerId { get; set; }

    public string PayScheduleId { get; set; }

    public string Type { get; set; }

    public DateOnly? PeriodStart { get; set; }

    public DateOnly? PeriodEnd { get; set; }

    public DateOnly? PayDate { get; set; }

    public string SupplementalMethod { get; set; }

    [BindNever]
    public IList<SelectListItem> Employers { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Schedules { get; set; } = [];
}

/// <summary>
/// An input typed on a pay run's page.
/// </summary>
public class PayRunInputViewModel
{
    public string StaffMemberId { get; set; }

    public string PayCodeId { get; set; }

    public string Hours { get; set; }

    public string Amount { get; set; }

    public string Rate { get; set; }

    public DateOnly? Date { get; set; }

    public string Note { get; set; }

    public bool IsNet { get; set; }

    public DateOnly? EarnedFrom { get; set; }

    public DateOnly? EarnedTo { get; set; }
}

/// <summary>
/// A pay run's page.
/// </summary>
public class PayRunDetailsViewModel
{
    [BindNever]
    public PayRun Run { get; set; }

    [BindNever]
    public string EmployerName { get; set; }

    [BindNever]
    public string ScheduleName { get; set; }

    [BindNever]
    public string Currency { get; set; }

    [BindNever]
    public IList<(PayRunInput Input, string StaffName, string Code)> Inputs { get; set; } = [];

    [BindNever]
    public IList<(PayRunIssue Issue, string StaffName)> Issues { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> StaffOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> CodeOptions { get; set; } = [];

    [BindNever]
    public bool CanPrepare { get; set; }

    [BindNever]
    public string ApprovalStatus { get; set; }

    [BindNever]
    public string ApprovalUrl { get; set; }

    [BindNever]
    public bool CanDecide { get; set; }

    [BindNever]
    public string DecideRefusal { get; set; }

    /// <summary>Gets or sets a value indicating whether the user may process the run.</summary>
    public bool CanProcess { get; set; }

    [BindNever]
    public AdminListContext Statements { get; set; }

    /// <summary>
    /// Gets or sets the open questions of employees about their pay: their name and note.
    /// </summary>
    public IList<(string StaffName, string Note)> Questions { get; set; } = [];

    public PayRunInputViewModel NewInput { get; set; } = new();
}

/// <summary>
/// A pay statement's page.
/// </summary>
public class PayStatementViewModel
{
    [BindNever]
    public PayStatement Statement { get; set; }

    [BindNever]
    public PayRun Run { get; set; }

    [BindNever]
    public IDictionary<string, string> Codes { get; set; } = new Dictionary<string, string>();

    /// <summary>Gets or sets a value indicating whether the user may void the statement and reissue its payments.</summary>
    public bool CanVoid { get; set; }

    /// <summary>Gets or sets the number of the statement this one reverses, or of the one that reversed it.</summary>
    public string LinkedNumber { get; set; }
}

/// <summary>
/// My Pay: the person's statements and pay profiles.
/// </summary>
public class MyPayIndexViewModel
{
    /// <summary>Gets or sets a value indicating whether the signed-in user is a staff member.</summary>
    public bool IsStaff { get; set; }

    /// <summary>Gets or sets a value indicating whether the person may change their elections and net pay split.</summary>
    public bool CanManageDetails { get; set; }

    /// <summary>Gets or sets a value indicating whether the person may manage their own bank accounts in Banking.</summary>
    public bool CanManageBankAccounts { get; set; }

    [BindNever]
    public AdminListContext Statements { get; set; }

    [BindNever]
    public IList<(EmployeePayProfile Profile, string EmployerName)> Profiles { get; set; } = [];

    [BindNever]
    public AdminListContext TaxForms { get; set; }

    [BindNever]
    public bool HasTaxForms { get; set; }

    /// <summary>
    /// Gets or sets the person's coming pay to review: the draft statement and its employer.
    /// </summary>
    public IList<(PayStatement Statement, string EmployerName)> ToReview { get; set; } = [];
}

/// <summary>
/// A row of My Pay's statements.
/// </summary>
public class MyPayReviewViewModel
{
    public PayStatementViewModel Statement { get; set; }

    public string EmployerName { get; set; }

    public PayStatementReview Review { get; set; }

    public bool IsCurrent { get; set; }

    public string Note { get; set; }
}

public sealed class MyPayStatementRow
{
    /// <summary>Gets or sets the statement.</summary>
    public PayStatement Statement { get; set; }

    /// <summary>Gets or sets the name of the employer that paid it.</summary>
    public string EmployerName { get; set; }
}

/// <summary>
/// My Pay: the person's tax elections and net pay split with one employer.
/// </summary>
public class MyPayDetailsViewModel
{
    [BindNever]
    public string EmployerName { get; set; }

    [BindNever]
    public IShape Editor { get; set; }
}
