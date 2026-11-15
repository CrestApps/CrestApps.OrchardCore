using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// What a line of pay is: an earning (regular, overtime, holiday...), a deduction, an employer contribution, imputed
/// income, a reimbursement or a memo. Workforce's time clock puts a pay code on each timesheet line; Payroll
/// calculates by them. Modules keep their own settings in <c>Properties</c>, such as Workforce's shift differential
/// window.
/// </summary>
public sealed class PayCode : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the short unique code, such as REG.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the class.
    /// </summary>
    public PayCodeClass Class { get; set; }

    /// <summary>
    /// Gets or sets what the line pays for, an open name: <see cref="PayCodeKinds"/> and those modules register.
    /// </summary>
    public string Kind { get; set; } = PayCodeKinds.Regular;

    /// <summary>
    /// Gets or sets what the hourly rate is multiplied by.
    /// </summary>
    public decimal Multiplier { get; set; } = 1;

    /// <summary>
    /// Gets or sets a premium paid an hour on top, or <see langword="null"/>.
    /// </summary>
    public decimal? PremiumPerHour { get; set; }

    /// <summary>
    /// Gets or sets the code an outside payroll system knows the line by, or <see langword="null"/> for <see cref="Code"/>.
    /// </summary>
    public string ExportCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the code is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the code an outside payroll system knows the line by.
    /// </summary>
    public string PayrollCode
        => string.IsNullOrEmpty(ExportCode) ? Code : ExportCode;
}

/// <summary>
/// The class of a pay code.
/// </summary>
public enum PayCodeClass
{
    /// <summary>
    /// Money earned: hours, salary, bonuses.
    /// </summary>
    Earning,

    /// <summary>
    /// Money taken from pay.
    /// </summary>
    Deduction,

    /// <summary>
    /// Money the employer adds, such as a retirement match.
    /// </summary>
    EmployerContribution,

    /// <summary>
    /// A value that is taxed but not paid, such as a benefit.
    /// </summary>
    ImputedIncome,

    /// <summary>
    /// Money paid back, not taxed.
    /// </summary>
    Reimbursement,

    /// <summary>
    /// A line shown for information only.
    /// </summary>
    Memo,
}

/// <summary>
/// The kinds of pay code the solution knows; a module adds its own with <c>AddPayCodeKind</c>.
/// </summary>
public static class PayCodeKinds
{
    /// <summary>Regular hours.</summary>
    public const string Regular = "Regular";

    /// <summary>Overtime hours.</summary>
    public const string Overtime = "Overtime";

    /// <summary>Double time hours.</summary>
    public const string DoubleTime = "DoubleTime";

    /// <summary>Holiday hours.</summary>
    public const string Holiday = "Holiday";

    /// <summary>Paid time off.</summary>
    public const string PaidTimeOff = "PaidTimeOff";

    /// <summary>Predictability pay.</summary>
    public const string PredictabilityPay = "PredictabilityPay";

    /// <summary>Call back hours.</summary>
    public const string CallBack = "CallBack";

    /// <summary>A premium for hours in a window.</summary>
    public const string ShiftDifferential = "ShiftDifferential";

    /// <summary>Unpaid time.</summary>
    public const string Unpaid = "Unpaid";

    /// <summary>Weekend hours.</summary>
    public const string Weekend = "Weekend";

    /// <summary>A bonus.</summary>
    public const string Bonus = "Bonus";

    /// <summary>A commission.</summary>
    public const string Commission = "Commission";

    /// <summary>Tips.</summary>
    public const string Tips = "Tips";

    /// <summary>A salary.</summary>
    public const string Salary = "Salary";

    /// <summary>Money paid back, such as expenses.</summary>
    public const string Reimbursement = "Reimbursement";

    /// <summary>The repayment of a loan or an advance.</summary>
    public const string Loan = "Loan";

    /// <summary>A contribution to a plan, by the person or the employer.</summary>
    public const string Contribution = "Contribution";

    /// <summary>Pay withheld for a garnishment order.</summary>
    public const string Garnishment = "Garnishment";

    /// <summary>Pay that brings a tipped person's week up to the minimum wage.</summary>
    public const string MinimumWageMakeup = "MinimumWageMakeup";

    /// <summary>
    /// The kinds Workforce's pay codes had, in their stored order, to read records written before.
    /// </summary>
    public static IReadOnlyList<string> LegacyOrder { get; } =
    [
        Regular, Overtime, DoubleTime, Holiday, PaidTimeOff, PredictabilityPay, CallBack, ShiftDifferential, Unpaid, Weekend,
    ];
}
