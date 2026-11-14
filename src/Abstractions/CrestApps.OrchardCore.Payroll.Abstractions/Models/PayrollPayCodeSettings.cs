using CrestApps.Core;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// How Payroll calculates and taxes a pay code, kept in the code's properties by the Payroll card of the pay code
/// editor. A code with none stored gets <see cref="PayrollPayCodeSettingsExtensions.GetPayrollSettings"/>'s defaults
/// for its class and kind.
/// </summary>
public sealed class PayrollPayCodeSettings
{
    /// <summary>
    /// Gets or sets how the amount of a line is found.
    /// </summary>
    public PayCodeCalculation Calculation { get; set; }

    /// <summary>
    /// Gets or sets the pay codes a percent is taken of, or none for every earning.
    /// </summary>
    public IList<string> BaseCodeIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the treatment of the code for each tax category; a category left out takes the class's default
    /// (<see cref="PayrollPayCodeSettingsExtensions.DefaultTreatment"/>).
    /// </summary>
    public Dictionary<string, PayCodeTaxTreatment> TaxTreatment { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets a value indicating whether the amount is paid apart from regular wages (a bonus, a commission),
    /// which a tax may withhold at its supplemental rate.
    /// </summary>
    public bool Supplemental { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the amount counts in the regular rate an overtime premium is priced on.
    /// </summary>
    public bool CountsInRegularRate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the hours count as hours worked (for overtime and benefits).
    /// </summary>
    public bool CountsAsHoursWorked { get; set; }

    /// <summary>
    /// Gets or sets the most a person can get or give by this code in one pay period, or <see langword="null"/>.
    /// </summary>
    public decimal? PeriodLimit { get; set; }

    /// <summary>
    /// Gets or sets the most a person can get or give by this code in a calendar year, from a day on (a yearly
    /// statutory limit changes from one year to the next).
    /// </summary>
    public IList<DatedAmount> YearLimits { get; set; } = [];

    /// <summary>
    /// Gets or sets the most a person can get or give by this code ever, or <see langword="null"/>.
    /// </summary>
    public decimal? LifetimeLimit { get; set; }

    /// <summary>
    /// Gets or sets the order deductions are taken in, smallest first; when net pay is too small the largest go first.
    /// </summary>
    public int Priority { get; set; } = PayrollPayCodeSettingsExtensions.DefaultPriority;

    /// <summary>
    /// Gets or sets what happens to the part of a deduction net pay could not cover.
    /// </summary>
    public PayCodeArrearsRule Arrears { get; set; }

    /// <summary>
    /// Gets or sets how a fixed amount is cut for a period the person worked only part of.
    /// </summary>
    public PayCodeProrationRule Proration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether retro pay recalculates the code for past periods.
    /// </summary>
    public bool InRetroPay { get; set; } = true;

    /// <summary>
    /// Gets or sets the boxes of tax forms the amounts go to, by the names the country pack registers.
    /// </summary>
    public IList<string> FormBoxes { get; set; } = [];

    /// <summary>
    /// Gets the yearly limit in effect on a day: the latest one started.
    /// </summary>
    /// <param name="day">The day, such as a pay date.</param>
    /// <returns>The limit, or <see langword="null"/> when none has started.</returns>
    public decimal? YearLimitOn(DateOnly day)
        => YearLimits.Where(limit => limit.EffectiveFrom <= day).MaxBy(limit => limit.EffectiveFrom)?.Amount;
}

/// <summary>
/// An amount from a day on.
/// </summary>
public sealed class DatedAmount
{
    /// <summary>
    /// Gets or sets the first day the amount applies.
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// How the amount of a pay code's line is found.
/// </summary>
public enum PayCodeCalculation
{
    /// <summary>
    /// Hours times the rate, times the code's multiplier, plus its premium an hour.
    /// </summary>
    HoursByRate,

    /// <summary>
    /// An amount: from a pay item or an input.
    /// </summary>
    FixedAmount,

    /// <summary>
    /// A percent of the earnings of the base codes (every earning when none is chosen).
    /// </summary>
    PercentOfEarnings,

    /// <summary>
    /// A percent of disposable earnings: gross less the taxes and deductions the law requires. Deductions only.
    /// </summary>
    PercentOfDisposableEarnings,
}

/// <summary>
/// How a pay code counts for one tax category.
/// </summary>
public enum PayCodeTaxTreatment
{
    /// <summary>
    /// An earning adds to the category's wages; a deduction is taken after the tax.
    /// </summary>
    Taxed,

    /// <summary>
    /// An earning does not add to the category's wages; a deduction is taken before the tax and lowers them.
    /// </summary>
    Exempt,
}

/// <summary>
/// What happens to the part of a deduction net pay could not cover.
/// </summary>
public enum PayCodeArrearsRule
{
    /// <summary>
    /// It is dropped.
    /// </summary>
    None,

    /// <summary>
    /// It is owed and taken on the next pay, on top of the usual amount.
    /// </summary>
    CarryForward,
}

/// <summary>
/// How a fixed amount is cut for a period the person worked only part of (hired, left or changed pay in it).
/// </summary>
public enum PayCodeProrationRule
{
    /// <summary>
    /// The whole amount.
    /// </summary>
    None,

    /// <summary>
    /// By the working days of the period the person was employed.
    /// </summary>
    WorkingDays,

    /// <summary>
    /// By the calendar days of the period the person was employed.
    /// </summary>
    CalendarDays,
}

/// <summary>
/// The tax categories every country has; a country pack adds its own with <c>AddPayrollTaxCategory</c>.
/// </summary>
public static class PayrollTaxCategories
{
    /// <summary>Income tax withheld from pay.</summary>
    public const string IncomeTax = "IncomeTax";

    /// <summary>Social insurance (pensions, health), paid by the employee, the employer or both.</summary>
    public const string SocialInsurance = "SocialInsurance";

    /// <summary>Unemployment insurance.</summary>
    public const string Unemployment = "Unemployment";
}

/// <summary>
/// Reads the Payroll settings of a pay code.
/// </summary>
public static class PayrollPayCodeSettingsExtensions
{
    /// <summary>
    /// The priority of a deduction nobody ordered.
    /// </summary>
    public const int DefaultPriority = 100;

    /// <summary>
    /// Gets the code's Payroll settings: the stored ones, else the defaults for its class and kind.
    /// </summary>
    /// <param name="code">The pay code.</param>
    /// <returns>The settings.</returns>
    public static PayrollPayCodeSettings GetPayrollSettings(this PayCode code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return code.TryGet<PayrollPayCodeSettings>(out var stored) && stored is not null ? stored : DefaultsFor(code);
    }

    /// <summary>
    /// Gets how a code counts for a tax category: its own treatment, else the class's default.
    /// </summary>
    /// <param name="code">The pay code.</param>
    /// <param name="category">The tax category.</param>
    /// <returns>The treatment.</returns>
    public static PayCodeTaxTreatment TreatmentFor(this PayCode code, string category)
        => code.GetPayrollSettings().TaxTreatment.TryGetValue(category, out var treatment) ? treatment : DefaultTreatment(code.Class);

    /// <summary>
    /// Gets how a class counts for every tax category unless a code says otherwise: earnings and imputed income are
    /// taxed and deductions taken after tax; employer contributions, reimbursements and memo lines are not taxed.
    /// </summary>
    /// <param name="payCodeClass">The class.</param>
    /// <returns>The treatment.</returns>
    public static PayCodeTaxTreatment DefaultTreatment(PayCodeClass payCodeClass)
        => payCodeClass is PayCodeClass.Earning or PayCodeClass.ImputedIncome or PayCodeClass.Deduction
            ? PayCodeTaxTreatment.Taxed
            : PayCodeTaxTreatment.Exempt;

    /// <summary>
    /// Gets the settings a code has until someone sets them: hours by rate for hours worked, an amount for the rest,
    /// and the flags the kind implies.
    /// </summary>
    /// <param name="code">The pay code.</param>
    /// <returns>The defaults.</returns>
    public static PayrollPayCodeSettings DefaultsFor(PayCode code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var hourly = code.Class == PayCodeClass.Earning && code.Kind is PayCodeKinds.Regular or PayCodeKinds.Overtime or PayCodeKinds.DoubleTime
            or PayCodeKinds.Holiday or PayCodeKinds.PaidTimeOff or PayCodeKinds.PredictabilityPay or PayCodeKinds.CallBack
            or PayCodeKinds.ShiftDifferential or PayCodeKinds.Weekend;

        return new PayrollPayCodeSettings
        {
            Calculation = hourly ? PayCodeCalculation.HoursByRate : PayCodeCalculation.FixedAmount,
            Supplemental = code.Kind is PayCodeKinds.Bonus or PayCodeKinds.Commission,

            // Pay for hours worked and pay promised for work (bonuses, commissions) count in the regular rate; pay for
            // hours not worked and overtime premiums do not.
            CountsInRegularRate = code.Class == PayCodeClass.Earning && code.Kind is PayCodeKinds.Regular or PayCodeKinds.ShiftDifferential
                or PayCodeKinds.Weekend or PayCodeKinds.CallBack or PayCodeKinds.Bonus or PayCodeKinds.Commission,
            // A shift differential pays a premium on hours another line already pays, so its hours are not counted again.
            CountsAsHoursWorked = code.Class == PayCodeClass.Earning && code.Kind is PayCodeKinds.Regular or PayCodeKinds.Overtime
                or PayCodeKinds.DoubleTime or PayCodeKinds.Weekend or PayCodeKinds.CallBack,
            Proration = code.Kind == PayCodeKinds.Salary ? PayCodeProrationRule.WorkingDays : PayCodeProrationRule.None,
            InRetroPay = code.Class is PayCodeClass.Earning or PayCodeClass.Deduction or PayCodeClass.EmployerContribution,
        };
    }
}
