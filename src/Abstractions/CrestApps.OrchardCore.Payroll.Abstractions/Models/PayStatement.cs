using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// What one person is paid by one employer on one pay date: the lines of the calculation, the payments of net pay and
/// the names and addresses as printed. A statement never changes once final: a correction is a reversing statement
/// (<see cref="VoidOf"/>) and a new one. Accumulators are the sums of the lines of statements that are not drafts.
/// </summary>
public sealed class PayStatement : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the number, given when the statement becomes final.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the pay run the statement belongs to.
    /// </summary>
    public string RunId { get; set; }

    /// <summary>
    /// Gets or sets the pay profile the statement was calculated from.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the staff member paid.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets the employer that pays.
    /// </summary>
    public string EmployerId { get; set; }

    /// <summary>
    /// Gets or sets the pay date.
    /// </summary>
    public DateOnly PayDate { get; set; }

    /// <summary>
    /// Gets or sets the first day of the pay period.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the last day of the pay period.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the three-letter currency code of the amounts.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the gross pay: earnings, imputed income excluded.
    /// </summary>
    public decimal Gross { get; set; }

    /// <summary>
    /// Gets or sets the net pay: what the person receives.
    /// </summary>
    public decimal Net { get; set; }

    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    public IList<PayStatementLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets how net pay is paid: one payment per row of the profile's split.
    /// </summary>
    public IList<PayStatementPayment> Payments { get; set; } = [];

    /// <summary>
    /// Gets or sets the warnings of the calculation, such as a deduction cut because pay was too small.
    /// </summary>
    public IList<string> Warnings { get; set; } = [];

    /// <summary>
    /// Gets or sets the balances shown on the statement, such as time off, as of the pay date.
    /// </summary>
    public IList<PayStatementBalance> Balances { get; set; } = [];

    /// <summary>
    /// Gets or sets the employee's review of the draft before the run was submitted, or <see langword="null"/>.
    /// </summary>
    public PayStatementReview Review { get; set; }

    /// <summary>
    /// Gets or sets whether the statement observers were told the statement was issued, so they hear it once even when a
    /// run is paid in several goes.
    /// </summary>
    public bool IssuedObserved { get; set; }

    /// <summary>
    /// Gets or sets the statement this one reverses, or <see langword="null"/>.
    /// </summary>
    public string VoidOf { get; set; }

    /// <summary>
    /// Gets or sets the statement that reversed this one, or <see langword="null"/>.
    /// </summary>
    public string VoidedBy { get; set; }

    /// <summary>
    /// Gets or sets the names and addresses as printed on the statement, kept as they were on the pay date.
    /// </summary>
    public PayStatementSnapshot Snapshot { get; set; } = new();

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public PayStatementStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it was calculated.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it became final.
    /// </summary>
    public DateTime? ProcessedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// A balance a pay statement shows, such as time off, as of its pay date.
/// </summary>
public sealed class PayStatementBalance
{
    /// <summary>
    /// The unit of a balance kept in hours.
    /// </summary>
    public const string Hours = "Hours";

    /// <summary>
    /// The unit of a balance kept in days.
    /// </summary>
    public const string Days = "Days";

    /// <summary>
    /// Gets or sets what the balance is, such as Vacation.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets its unit, <see cref="Hours"/> or <see cref="Days"/>.
    /// </summary>
    public string Unit { get; set; } = Hours;

    /// <summary>
    /// Gets or sets the balance.
    /// </summary>
    public decimal Balance { get; set; }
}

/// <summary>
/// An employee's review of their draft pay before the run is submitted.
/// </summary>
public sealed class PayStatementReview
{
    /// <summary>
    /// Gets or sets what the employee said.
    /// </summary>
    public PayStatementReviewStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the employee's note, for a question.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the net pay the employee saw; a calculation that changes the net makes the review out of date.
    /// </summary>
    public decimal ReviewedNet { get; set; }

    /// <summary>
    /// Gets or sets the user who reviewed.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets when, in UTC.
    /// </summary>
    public DateTime? ReviewedUtc { get; set; }

    /// <summary>
    /// Gets or sets the net pay the employee was last told to review, so a calculation tells them again only when it
    /// changed.
    /// </summary>
    public decimal? NotifiedNet { get; set; }

    /// <summary>
    /// Gets whether the review is about the statement's current net pay.
    /// </summary>
    /// <param name="net">The statement's net pay.</param>
    /// <returns><see langword="true"/> when the employee reviewed this net pay.</returns>
    public bool IsCurrentFor(decimal net)
        => Status != PayStatementReviewStatus.Pending && ReviewedNet == net;
}

/// <summary>
/// What an employee said of their draft pay.
/// </summary>
public enum PayStatementReviewStatus
{
    /// <summary>Asked, not answered.</summary>
    Pending,

    /// <summary>The employee says it is right.</summary>
    Confirmed,

    /// <summary>The employee says something is wrong.</summary>
    Questioned,
}

/// <summary>
/// A line of a pay statement: an amount by a pay code, or a tax.
/// </summary>
public sealed class PayStatementLine
{
    /// <summary>
    /// Gets or sets what the line is.
    /// </summary>
    public PayLineType Type { get; set; }

    /// <summary>
    /// Gets or sets the pay code, or <see langword="null"/> for a tax.
    /// </summary>
    public string PayCodeId { get; set; }

    /// <summary>
    /// Gets or sets the tax's code (such as a provider's name for it), or <see langword="null"/> for a pay code.
    /// </summary>
    public string TaxCode { get; set; }

    /// <summary>
    /// Gets or sets the tax category of a tax line.
    /// </summary>
    public string TaxCategory { get; set; }

    /// <summary>
    /// Gets or sets what the statement shows.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the hours, or <see langword="null"/>.
    /// </summary>
    public decimal? Hours { get; set; }

    /// <summary>
    /// Gets or sets the rate an hour, or <see langword="null"/>.
    /// </summary>
    public decimal? Rate { get; set; }

    /// <summary>
    /// Gets or sets the amount, always zero or more: the type says whether it is paid or taken (a reversing statement
    /// has negative amounts).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction of a tax line, or <see langword="null"/>.
    /// </summary>
    public string Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets who pays: the employee (taken from pay) or the employer (on top of pay).
    /// </summary>
    public PayLinePayer Payer { get; set; }

    /// <summary>
    /// Gets or sets the wages a tax line was worked out on.
    /// </summary>
    public decimal? TaxableWages { get; set; }

    /// <summary>
    /// Gets or sets what a limit or net pay cut off this line, or zero.
    /// </summary>
    public decimal Cut { get; set; }

    /// <summary>
    /// Gets or sets what a deduction could not take and is owed for the next pay, or zero.
    /// </summary>
    public decimal ArrearsAdded { get; set; }

    /// <summary>
    /// Gets or sets the arrears of the line's pay item before the statement became final, so a void puts it back.
    /// </summary>
    public decimal? ArrearsBefore { get; set; }

    /// <summary>
    /// Gets or sets the pay item the line comes from, or <see langword="null"/>.
    /// </summary>
    public string PayItemId { get; set; }

    /// <summary>
    /// Gets or sets the garnishment order the line withholds for, or <see langword="null"/>.
    /// </summary>
    public string GarnishmentOrderId { get; set; }

    /// <summary>
    /// Gets or sets the earlier statement a retro pay line makes up the difference of, or <see langword="null"/>.
    /// </summary>
    public string RetroFor { get; set; }

    /// <summary>
    /// Gets or sets where the cost goes.
    /// </summary>
    public PayInputAllocation Allocation { get; set; }

    /// <summary>
    /// Gets or sets the inputs the line comes from.
    /// </summary>
    public IList<PayInputReference> Sources { get; set; } = [];
}

/// <summary>
/// What a statement line is.
/// </summary>
public enum PayLineType
{
    /// <summary>Money earned.</summary>
    Earning,

    /// <summary>Money taken from pay.</summary>
    Deduction,

    /// <summary>Money the employer adds, not paid to the person.</summary>
    EmployerContribution,

    /// <summary>A value taxed but not paid.</summary>
    ImputedIncome,

    /// <summary>Money paid back, not taxed.</summary>
    Reimbursement,

    /// <summary>A line shown only.</summary>
    Memo,

    /// <summary>A tax.</summary>
    Tax,
}

/// <summary>
/// Who pays a line.
/// </summary>
public enum PayLinePayer
{
    /// <summary>The employee: it is taken from pay.</summary>
    Employee,

    /// <summary>The employer: it is paid on top of pay.</summary>
    Employer,
}

/// <summary>
/// A payment of net pay.
/// </summary>
public sealed class PayStatementPayment
{
    /// <summary>
    /// Gets or sets how it is paid.
    /// </summary>
    public NetPayMethod Method { get; set; }

    /// <summary>
    /// Gets or sets the bank account of a deposit.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the disbursement or check that paid it, once paid.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets a short text for the statement, such as a check number.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets how many times the payment was paid (a reissue pays it again), for its idempotency key.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Gets or sets why the payment could not be paid, or <see langword="null"/>.
    /// </summary>
    public string Problem { get; set; }
}

/// <summary>
/// The names and addresses printed on a statement.
/// </summary>
public sealed class PayStatementSnapshot
{
    /// <summary>Gets or sets the person's name as printed.</summary>
    public string EmployeeName { get; set; }

    /// <summary>Gets or sets the person's employee number.</summary>
    public string EmployeeNumber { get; set; }

    /// <summary>Gets or sets the last characters of the tax id.</summary>
    public string TaxIdHint { get; set; }

    /// <summary>Gets or sets the employer's legal name.</summary>
    public string EmployerName { get; set; }

    /// <summary>Gets or sets the person's address, one line each.</summary>
    public IList<string> EmployeeAddress { get; set; } = [];

    /// <summary>Gets or sets the employer's address, one line each.</summary>
    public IList<string> EmployerAddress { get; set; } = [];
}

/// <summary>
/// The status of a statement.
/// </summary>
public enum PayStatementStatus
{
    /// <summary>Calculated, not final: it changes when its run is calculated again.</summary>
    Draft,

    /// <summary>Final: it never changes; its lines count in the accumulators.</summary>
    Final,

    /// <summary>Final and paid.</summary>
    Paid,

    /// <summary>Reversed by a void statement; both count, so they cancel out.</summary>
    Voided,
}
