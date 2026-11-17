using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// A deposit of payroll taxes the business made to an agency for a liability period, recorded so the liabilities show
/// what is left to pay. Payroll never moves the money: the business deposits and records it here.
/// </summary>
public sealed class TaxDeposit : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the jurisdiction deposited to, such as US.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the group of taxes (<see cref="TaxLiabilityGroups"/>).</summary>
    public string Group { get; set; } = TaxLiabilityGroups.Withholding;

    /// <summary>Gets or sets the first day of the liability period paid.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Gets or sets the last day of the liability period paid.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Gets or sets the amount deposited.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the day it was deposited.</summary>
    public DateOnly PaidOn { get; set; }

    /// <summary>Gets or sets the agency's confirmation number.</summary>
    public string Reference { get; set; }

    /// <summary>Gets or sets who recorded it.</summary>
    public string RecordedBy { get; set; }

    /// <summary>Gets or sets when it was recorded.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// The groups payroll taxes are deposited in: the taxes withheld and matched (income tax, social insurance), and
/// unemployment taxes, which most countries collect apart.
/// </summary>
public static class TaxLiabilityGroups
{
    /// <summary>Income tax withheld and social insurance of both sides.</summary>
    public const string Withholding = "Withholding";

    /// <summary>Unemployment taxes.</summary>
    public const string Unemployment = "Unemployment";

    /// <summary>
    /// Taxes withheld from payments that are not wages, such as backup withholding on a contractor's payments: deposited
    /// and filed apart from the payroll taxes (Form 945 in the United States).
    /// </summary>
    public const string NonPayrollWithholding = "NonPayrollWithholding";
}

/// <summary>
/// How often an employer deposits a jurisdiction's withholding, stored as text on its registration.
/// </summary>
public static class DepositSchedules
{
    /// <summary>By the 15th of the next month.</summary>
    public const string Monthly = "Monthly";

    /// <summary>Pay dates Wednesday to Friday by the next Wednesday; Saturday to Tuesday by the next Friday.</summary>
    public const string SemiWeekly = "SemiWeekly";

    /// <summary>By the last day of the month after the quarter.</summary>
    public const string Quarterly = "Quarterly";

    /// <summary>By the last day of January of the next year.</summary>
    public const string Annual = "Annual";

    /// <summary>
    /// Every quarter, by the 15th of the month after it (a Canadian quarterly remitter).
    /// </summary>
    public const string QuarterlyMidMonth = "QuarterlyMidMonth";

    /// <summary>
    /// Twice a month: the 1st to the 15th by the 25th, the 16th to the month's end by the 10th of the next month (a
    /// Canadian accelerated remitter, threshold 1).
    /// </summary>
    public const string TwiceMonthly = "TwiceMonthly";

    /// <summary>
    /// Four times a month: the 1st to the 7th, the 8th to the 14th, the 15th to the 21st and the 22nd to the month's
    /// end, each by the third working day after it (a Canadian accelerated remitter, threshold 2).
    /// </summary>
    public const string FourTimesMonthly = "FourTimesMonthly";
}

/// <summary>
/// A period of payroll taxes owed to a jurisdiction: what the statements with pay dates in it owe (both sides), what
/// was deposited, what is left and when it is due. Derived from the statements and the deposits; never stored.
/// </summary>
public sealed class TaxLiabilityPeriod
{
    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the jurisdiction.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the group of taxes.</summary>
    public string Group { get; set; }

    /// <summary>Gets or sets the deposit schedule the period follows.</summary>
    public string Schedule { get; set; }

    /// <summary>Gets or sets the first day.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Gets or sets the last day.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Gets or sets the day the deposit is due.</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>Gets or sets the currency.</summary>
    public string Currency { get; set; }

    /// <summary>Gets or sets what the employees' taxes withheld add.</summary>
    public decimal EmployeeTaxes { get; set; }

    /// <summary>Gets or sets what the employer's taxes add.</summary>
    public decimal EmployerTaxes { get; set; }

    /// <summary>Gets or sets what was deposited.</summary>
    public decimal Deposited { get; set; }

    /// <summary>Gets the amount owed.</summary>
    public decimal Owed
        => EmployeeTaxes + EmployerTaxes;

    /// <summary>Gets what is left to deposit (below zero when more was deposited).</summary>
    public decimal Balance
        => Owed - Deposited;

    /// <summary>Gets a stable key of the period.</summary>
    public string Key
        => $"{EmployerId}:{Jurisdiction}:{Group}:{PeriodStart:yyyyMMdd}";
}

/// <summary>
/// The totals of a tax form for a period, prepared from the final statements: a quarterly or annual return, or the
/// year's wage statements of each person with their transmittal. Filed by the business; amended by a new filing that
/// names the one it corrects.
/// </summary>
public sealed class PayrollFiling : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the form's stored name, such as US-941.</summary>
    public string Form { get; set; }

    /// <summary>Gets or sets the year.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the quarter, 1 to 4, of a quarterly form, or 0.</summary>
    public int Quarter { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public PayrollFilingStatus Status { get; set; }

    /// <summary>Gets or sets the form's boxes, by their number, such as "2" or "5a".</summary>
    public Dictionary<string, decimal> Boxes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets each person's statement of a form that has one per person, such as a wage statement.</summary>
    public IList<PayrollFilingPerson> People { get; set; } = [];

    /// <summary>Gets or sets the notes the form found, such as a person with no tax id.</summary>
    public IList<string> Notes { get; set; } = [];

    /// <summary>Gets or sets who prepared it last.</summary>
    public string PreparedBy { get; set; }

    /// <summary>Gets or sets when it was prepared last.</summary>
    public DateTime? PreparedUtc { get; set; }

    /// <summary>Gets or sets the day it was filed.</summary>
    public DateOnly? FiledOn { get; set; }

    /// <summary>Gets or sets the agency's confirmation number.</summary>
    public string Reference { get; set; }

    /// <summary>Gets or sets the filing this one corrects, or <see langword="null"/>.</summary>
    public string AmendsId { get; set; }

    /// <summary>Gets or sets when it was added.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>Gets the period's name, such as 2026 Q4 or 2026.</summary>
    public string Period
        => Quarter > 0 ? $"{Year} Q{Quarter}" : $"{Year}";
}

/// <summary>
/// The status of a filing.
/// </summary>
public enum PayrollFilingStatus
{
    /// <summary>Prepared and open to preparing again.</summary>
    Draft,

    /// <summary>Filed by the business; never changed again.</summary>
    Filed,

    /// <summary>Filed, then corrected by a later filing.</summary>
    Amended,
}

/// <summary>
/// One person's statement of a filing, such as a wage statement: the boxes and the names as printed. The tax id is
/// never stored here; it is read from the pay profile when a file needs it.
/// </summary>
public sealed class PayrollFilingPerson
{
    /// <summary>Gets or sets the staff member.</summary>
    public string StaffMemberId { get; set; }

    /// <summary>Gets or sets the pay profile.</summary>
    public string ProfileId { get; set; }

    /// <summary>Gets or sets the name as printed.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the address lines as printed.</summary>
    public IList<string> Address { get; set; } = [];

    /// <summary>Gets or sets the last characters of the tax id, as printed.</summary>
    public string TaxIdHint { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction the statement is for, when a person has one statement per jurisdiction, such as a
    /// T4 slip per province of employment (its box 10): stored when the filing is prepared, as statements said it then.
    /// </summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the boxes.</summary>
    public Dictionary<string, decimal> Boxes { get; set; } = new(StringComparer.Ordinal);
}
