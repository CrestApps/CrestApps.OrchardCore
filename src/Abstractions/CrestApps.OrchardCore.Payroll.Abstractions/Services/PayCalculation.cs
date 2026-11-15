using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Staff.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Everything one person's pay is calculated from, and what the calculation found. The calculation is a pure function
/// of it: <see cref="IPayCalculator"/> runs the <see cref="IPayCalculationStep"/>s in order, and each step reads the
/// inputs and the lines the steps before it added.
/// </summary>
public sealed class PayCalculationContext
{
    /// <summary>Gets or sets the pay profile.</summary>
    public EmployeePayProfile Profile { get; set; }

    /// <summary>Gets or sets the employer.</summary>
    public Employer Employer { get; set; }

    /// <summary>Gets or sets the pay schedule.</summary>
    public PaySchedule Schedule { get; set; }

    /// <summary>Gets or sets the first day of the pay period.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Gets or sets the last day of the pay period.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Gets or sets the pay date.</summary>
    public DateOnly PayDate { get; set; }

    /// <summary>Gets or sets the staff member's pay: basis, dated rates and salaries.</summary>
    public StaffPayRates Pay { get; set; } = new();

    /// <summary>Gets or sets the first day the person works, or <see langword="null"/>.</summary>
    public DateOnly? HireDate { get; set; }

    /// <summary>Gets or sets the last day the person works, or <see langword="null"/>.</summary>
    public DateOnly? TerminationDate { get; set; }

    /// <summary>Gets or sets the hours and amounts of the period, from every pay input source and typed in.</summary>
    public IList<PayInput> Inputs { get; set; } = [];

    /// <summary>Gets or sets the profile's pay items.</summary>
    public IList<EmployeePayItem> Items { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the pay items apply (a bonus run may leave them out).</summary>
    public bool IncludePayItems { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the salary for the period is paid (a bonus run leaves it out).</summary>
    public bool IncludeSalary { get; set; } = true;

    /// <summary>Gets or sets the pay codes, by id.</summary>
    public IReadOnlyDictionary<string, PayCode> Codes { get; set; } = new Dictionary<string, PayCode>();

    /// <summary>Gets or sets what was paid and taken before, from final statements.</summary>
    public PayAccumulators Accumulators { get; set; } = new();

    /// <summary>Gets or sets how supplemental pay is withheld.</summary>
    public PayrollSupplementalMethod SupplementalMethod { get; set; }

    /// <summary>Gets or sets the least net pay may be; deductions are cut below it.</summary>
    public decimal MinimumNet { get; set; }

    /// <summary>Gets or sets a gross-up: the earning to raise until net pay reaches a target, or <see langword="null"/>.</summary>
    public PayGrossUp GrossUp { get; set; }

    /// <summary>Gets or sets the jurisdictions of the person's home.</summary>
    public IList<string> HomeJurisdictions { get; set; } = [];

    /// <summary>Gets or sets the jurisdictions of the person's work site.</summary>
    public IList<string> WorkJurisdictions { get; set; } = [];

    /// <summary>Gets the currency of the amounts: the employer's.</summary>
    public string Currency
        => Employer?.Currency;

    /// <summary>Gets the lines the steps added.</summary>
    public List<PayStatementLine> Lines { get; } = [];

    /// <summary>Gets the warnings the steps added.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Gets the payments of net pay.</summary>
    public List<PayStatementPayment> Payments { get; } = [];

    /// <summary>
    /// Gets the gross pay: earnings.
    /// </summary>
    public decimal Gross
        => Lines.Where(line => line.Type == PayLineType.Earning).Sum(line => line.Amount);

    /// <summary>
    /// Gets net pay: earnings and reimbursements less deductions and the employee's taxes.
    /// </summary>
    public decimal Net
        => Lines.Sum(line => line.Type switch
        {
            PayLineType.Earning or PayLineType.Reimbursement => line.Amount,
            PayLineType.Deduction => -line.Amount,
            PayLineType.Tax when line.Payer == PayLinePayer.Employee => -line.Amount,
            _ => 0m,
        });

    /// <summary>
    /// Gets the pay code of an id, or <see langword="null"/>.
    /// </summary>
    /// <param name="payCodeId">The id.</param>
    /// <returns>The code.</returns>
    public PayCode Code(string payCodeId)
        => payCodeId is not null && Codes.TryGetValue(payCodeId, out var code) ? code : null;

    /// <summary>
    /// Rounds an amount to the currency's minor unit.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The rounded amount.</returns>
    public decimal Round(decimal amount)
        => string.IsNullOrEmpty(Currency) ? Math.Round(amount, 2, MidpointRounding.AwayFromZero) : CurrencyScale.Round(amount, Currency);

    /// <summary>
    /// Clears what a calculation found, to calculate again (a gross-up repeats the pipeline).
    /// </summary>
    public void Reset()
    {
        Lines.Clear();
        Warnings.Clear();
        Payments.Clear();
    }
}

/// <summary>
/// A gross-up: an earning raised until net pay reaches a target, such as a bonus promised net of taxes.
/// </summary>
public sealed class PayGrossUp
{
    /// <summary>Gets or sets the earning's pay code.</summary>
    public string PayCodeId { get; set; }

    /// <summary>Gets or sets the net pay to reach for the whole statement.</summary>
    public decimal TargetNet { get; set; }

    /// <summary>Gets or sets the gross of the earning found; set by the calculator.</summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// How supplemental pay (a bonus, a commission) is withheld for income tax.
/// </summary>
public enum PayrollSupplementalMethod
{
    /// <summary>Added to regular wages of the period and withheld together.</summary>
    Aggregate,

    /// <summary>Withheld at the tax's flat supplemental rate.</summary>
    Flat,
}

/// <summary>
/// A step of the pay calculation: earnings, regular rate, deductions before tax, taxable wages, taxes, deductions after
/// tax, garnishments, the net check, contributions and reimbursements, and the split of net pay. A module adds a step
/// with its own order or replaces one by registering a step with the same order and a higher priority.
/// </summary>
public interface IPayCalculationStep
{
    /// <summary>
    /// Gets the order the step runs in, smallest first (see <see cref="PayCalculationOrder"/>).
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Adds the step's lines, warnings or payments.
    /// </summary>
    /// <param name="context">The calculation.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// The orders of the built-in calculation steps.
/// </summary>
public static class PayCalculationOrder
{
    /// <summary>Salary, hours by rate, amounts and recurring earnings.</summary>
    public const int Earnings = 100;

    /// <summary>The regular rate and the overtime premium it requires.</summary>
    public const int RegularRate = 200;

    /// <summary>Deductions taken before at least one tax.</summary>
    public const int PreTaxDeductions = 300;

    /// <summary>Taxes, through the tax provider.</summary>
    public const int Taxes = 500;

    /// <summary>Deductions taken after every tax.</summary>
    public const int PostTaxDeductions = 600;

    /// <summary>Deductions that are a percent of disposable earnings, such as garnishments.</summary>
    public const int Garnishments = 700;

    /// <summary>Cuts deductions when net pay is too small.</summary>
    public const int NetCheck = 800;

    /// <summary>Employer contributions and reimbursements.</summary>
    public const int Contributions = 900;

    /// <summary>The split of net pay.</summary>
    public const int Distribution = 1000;
}

/// <summary>
/// Runs the calculation steps, repeating them for a gross-up.
/// </summary>
public interface IPayCalculator
{
    /// <summary>
    /// Calculates a person's pay for a period.
    /// </summary>
    /// <param name="context">The calculation; its lines, warnings and payments are filled.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task CalculateAsync(PayCalculationContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a person was paid and what was taken, by pay code and by tax, from the lines of final statements (drafts never
/// count). Derived, never stored: rebuilt from the statements whenever it is read.
/// </summary>
public sealed class PayAccumulators
{
    /// <summary>
    /// Gets the totals, by key (<see cref="CodeKey"/> or <see cref="TaxKey"/>).
    /// </summary>
    public Dictionary<string, PayAccumulator> Totals { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the totals of a pay code or a tax, or zeros.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The totals.</returns>
    public PayAccumulator For(string key)
        => Totals.TryGetValue(key, out var totals) ? totals : new PayAccumulator();

    /// <summary>
    /// Gets the key of a pay code's totals.
    /// </summary>
    /// <param name="payCodeId">The pay code's id.</param>
    /// <returns>The key.</returns>
    public static string CodeKey(string payCodeId)
        => "code:" + payCodeId;

    /// <summary>
    /// Gets the key of a tax's totals.
    /// </summary>
    /// <param name="taxCode">The tax's code.</param>
    /// <returns>The key.</returns>
    public static string TaxKey(string taxCode)
        => "tax:" + taxCode;
}

/// <summary>
/// The totals of a pay code or a tax.
/// </summary>
public sealed class PayAccumulator
{
    /// <summary>Gets or sets the amount in the pay period being calculated (earlier statements of the same period).</summary>
    public decimal Period { get; set; }

    /// <summary>Gets or sets the amount in the month of the pay date.</summary>
    public decimal Month { get; set; }

    /// <summary>Gets or sets the amount in the quarter of the pay date.</summary>
    public decimal Quarter { get; set; }

    /// <summary>Gets or sets the amount in the year of the pay date.</summary>
    public decimal Year { get; set; }

    /// <summary>Gets or sets the amount ever.</summary>
    public decimal Lifetime { get; set; }

    /// <summary>Gets or sets the hours in the year of the pay date.</summary>
    public decimal YearHours { get; set; }

    /// <summary>Gets or sets the taxable wages of a tax in the year of the pay date (for a wage base).</summary>
    public decimal YearTaxableWages { get; set; }

    /// <summary>
    /// Gets or sets the taxable wages of a tax in the pay period being calculated (earlier statements of the same
    /// period), so a second pay in a period is withheld on the period's total.
    /// </summary>
    public decimal PeriodTaxableWages { get; set; }
}

/// <summary>
/// Reads accumulators from final statements.
/// </summary>
public interface IPayAccumulatorService
{
    /// <summary>
    /// Gets what a person was paid and what was taken by an employer before a pay date.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="employerId">The employer.</param>
    /// <param name="payDate">The pay date: statements paid on or before it count.</param>
    /// <param name="periodStart">The first day of the period being calculated.</param>
    /// <param name="periodEnd">The last day of the period being calculated.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The accumulators.</returns>
    Task<PayAccumulators> GetAsync(string staffMemberId, string employerId, DateOnly payDate, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default);
}

/// <summary>
/// Works out the taxes of a pay. Stateless: everything it needs is in the request, so a commercial tax engine, a
/// full-service provider and the built-in tables all fit behind it.
/// </summary>
public interface IPayrollTaxProvider
{
    /// <summary>
    /// Gets the stored name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets what people see.
    /// </summary>
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Works out the taxes.
    /// </summary>
    /// <param name="request">The pay.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The tax lines.</returns>
    Task<PayrollTaxResult> CalculateAsync(PayrollTaxRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a tax provider needs to work out the taxes of a pay.
/// </summary>
public sealed class PayrollTaxRequest
{
    /// <summary>Gets or sets the employer.</summary>
    public Employer Employer { get; set; }

    /// <summary>Gets or sets the staff member.</summary>
    public string StaffMemberId { get; set; }

    /// <summary>Gets or sets the worker type: a contractor has nothing withheld.</summary>
    public PayWorkerType WorkerType { get; set; }

    /// <summary>Gets or sets the jurisdictions of the person's home.</summary>
    public IList<string> HomeJurisdictions { get; set; } = [];

    /// <summary>Gets or sets the jurisdictions of the work site.</summary>
    public IList<string> WorkJurisdictions { get; set; } = [];

    /// <summary>Gets or sets the elections in effect on the pay date, one per jurisdiction.</summary>
    public IList<TaxElection> Elections { get; set; } = [];

    /// <summary>Gets or sets the pay date.</summary>
    public DateOnly PayDate { get; set; }

    /// <summary>Gets or sets how often the person is paid.</summary>
    public PayFrequency Frequency { get; set; }

    /// <summary>Gets or sets the pay periods in a year of the frequency.</summary>
    public int PeriodsPerYear { get; set; }

    /// <summary>Gets or sets the regular wages of each tax category.</summary>
    public IDictionary<string, decimal> Wages { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>Gets or sets the supplemental wages of each tax category (part of neither <see cref="Wages"/>).</summary>
    public IDictionary<string, decimal> SupplementalWages { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>Gets or sets how supplemental wages are withheld.</summary>
    public PayrollSupplementalMethod SupplementalMethod { get; set; }

    /// <summary>
    /// Gets or sets the wages of each tax category paid this calendar year before this pay (regular and supplemental),
    /// for a tax that starts above a threshold.
    /// </summary>
    public IDictionary<string, decimal> YearWages { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the supplemental wages of each tax category paid this calendar year before this pay.
    /// </summary>
    public IDictionary<string, decimal> YearSupplementalWages { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>Gets or sets the accumulators, for wage bases and year-to-date limits.</summary>
    public PayAccumulators Accumulators { get; set; } = new();

    /// <summary>Gets or sets the currency of the amounts.</summary>
    public string Currency { get; set; }
}

/// <summary>
/// The taxes of a pay.
/// </summary>
public sealed class PayrollTaxResult
{
    /// <summary>Gets the tax lines.</summary>
    public IList<PayrollTaxLine> Lines { get; } = [];

    /// <summary>Gets the warnings, such as a jurisdiction with no rule.</summary>
    public IList<string> Warnings { get; } = [];
}

/// <summary>
/// A tax of a pay.
/// </summary>
public sealed class PayrollTaxLine
{
    /// <summary>Gets or sets the tax's code, stable across pays (accumulators sum by it).</summary>
    public string TaxCode { get; set; }

    /// <summary>Gets or sets what the statement shows.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the jurisdiction.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the tax category the wages were taken from.</summary>
    public string Category { get; set; }

    /// <summary>Gets or sets who pays.</summary>
    public PayLinePayer Payer { get; set; }

    /// <summary>Gets or sets the wages the tax was worked out on.</summary>
    public decimal TaxableWages { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public decimal Amount { get; set; }
}
