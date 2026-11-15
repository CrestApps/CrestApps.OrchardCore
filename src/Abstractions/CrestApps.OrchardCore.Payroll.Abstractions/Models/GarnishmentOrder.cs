using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// An order to withhold part of a person's pay and send it to a payee, such as child support, a tax levy or a creditor's
/// judgment. Each pay takes what the order asks, within the limit the law sets on disposable earnings (pay less the
/// employee's taxes), the orders with the lowest priority number first; what was withheld is sent to the payee by check
/// or by deposit to the payee's bank account.
/// </summary>
public sealed class GarnishmentOrder : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>Gets or sets the employer that withholds.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the pay profile the order applies to.</summary>
    public string ProfileId { get; set; }

    /// <summary>Gets or sets the staff member (from the profile).</summary>
    public string StaffMemberId { get; set; }

    /// <summary>Gets or sets the kind of order, which sets the default limit and priority.</summary>
    public GarnishmentType Type { get; set; }

    /// <summary>Gets or sets the case or docket number the payee needs with each payment.</summary>
    public string CaseNumber { get; set; }

    /// <summary>Gets or sets the court or agency that issued the order.</summary>
    public string Agency { get; set; }

    /// <summary>Gets or sets the deduction pay code the withholding is shown and totalled under.</summary>
    public string PayCodeId { get; set; }

    /// <summary>Gets or sets the amount each pay, or <see langword="null"/>.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Gets or sets the percent of disposable earnings each pay, or <see langword="null"/>.</summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets the most, as a percent of disposable earnings, the order may take with the orders before it of its
    /// limit group (support orders share theirs; other orders share the creditor limit).
    /// </summary>
    public decimal? LimitPercent { get; set; }

    /// <summary>
    /// Gets or sets the pay a week that must be left to the person, before a creditor order takes anything (30 times the
    /// federal minimum wage in the United States).
    /// </summary>
    public decimal? ProtectedWeeklyAmount { get; set; }

    /// <summary>Gets or sets the pay each period exempt from a tax levy, from the levy notice.</summary>
    public decimal? ExemptAmount { get; set; }

    /// <summary>Gets or sets whether the person supports another spouse or child not covered by a support order, which lowers the support limit from 60% to 50%.</summary>
    public bool OtherDependents { get; set; }

    /// <summary>Gets or sets whether the support order is for arrears more than 12 weeks old, which raises the support limit by 5 points.</summary>
    public bool ArrearsOver12Weeks { get; set; }

    /// <summary>Gets or sets the total owed, or <see langword="null"/> for an order with no end amount.</summary>
    public decimal? TotalOwed { get; set; }

    /// <summary>Gets or sets what was withheld so far, moved by processed and voided statements.</summary>
    public decimal Withheld { get; set; }

    /// <summary>Gets or sets the order's place among a person's orders: the lowest number is taken first.</summary>
    public int Priority { get; set; }

    /// <summary>Gets or sets the day the employer received the order (orders of the same priority by it).</summary>
    public DateOnly ReceivedDate { get; set; }

    /// <summary>Gets or sets the first pay date the order applies to.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Gets or sets the last pay date the order applies to, or <see langword="null"/>.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public GarnishmentStatus Status { get; set; }

    /// <summary>Gets or sets the payee's name, printed on checks.</summary>
    public string PayeeName { get; set; }

    /// <summary>Gets or sets the payee's mailing address, for checks.</summary>
    public Address PayeeAddress { get; set; }

    /// <summary>Gets or sets how the payee is paid.</summary>
    public GarnishmentPaymentMethod PaymentMethod { get; set; }

    /// <summary>Gets or sets the payee's bank account (Banking), for a deposit.</summary>
    public string PayeeBankAccountId { get; set; }

    /// <summary>Gets or sets what was sent to the payee, one row a statement.</summary>
    public IList<GarnishmentRemittance> Remittances { get; set; } = [];

    /// <summary>Gets or sets notes.</summary>
    public string Notes { get; set; }

    /// <summary>Gets or sets when it was added.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets what is left to withhold, or <see langword="null"/> when the order has no end amount.
    /// </summary>
    public decimal? Remaining
        => TotalOwed is { } total ? Math.Max(0, total - Withheld) : null;

    /// <summary>
    /// Gets whether the order takes from a pay on a pay date.
    /// </summary>
    /// <param name="payDate">The pay date.</param>
    /// <returns><see langword="true"/> when it applies.</returns>
    public bool AppliesOn(DateOnly payDate)
        => Status == GarnishmentStatus.Active && StartDate <= payDate && (EndDate is null || EndDate >= payDate) && Remaining is not 0;
}

/// <summary>
/// The kind of a garnishment order.
/// </summary>
public enum GarnishmentType
{
    /// <summary>Child or spousal support.</summary>
    Support,

    /// <summary>A federal tax levy.</summary>
    FederalTaxLevy,

    /// <summary>A state or local tax levy.</summary>
    StateTaxLevy,

    /// <summary>A bankruptcy court's order (a set amount, no limit).</summary>
    Bankruptcy,

    /// <summary>A defaulted student loan (administrative wage garnishment).</summary>
    StudentLoan,

    /// <summary>A creditor's judgment.</summary>
    Creditor,

    /// <summary>Another order.</summary>
    Other,
}

/// <summary>
/// The status of a garnishment order.
/// </summary>
public enum GarnishmentStatus
{
    /// <summary>Withheld from each pay.</summary>
    Active,

    /// <summary>Held: nothing is withheld until it is active again.</summary>
    Suspended,

    /// <summary>The total owed was withheld.</summary>
    Completed,

    /// <summary>Ended by the payee or the court.</summary>
    Released,
}

/// <summary>
/// How a garnishment payee is paid.
/// </summary>
public enum GarnishmentPaymentMethod
{
    /// <summary>A check on the employer's funding account.</summary>
    Check,

    /// <summary>A deposit to the payee's bank account.</summary>
    BankAccount,

    /// <summary>Paid outside payroll, such as through a state's portal: only recorded.</summary>
    Outside,
}

/// <summary>
/// What a statement sent to a garnishment payee.
/// </summary>
public sealed class GarnishmentRemittance
{
    /// <summary>Gets or sets the statement.</summary>
    public string StatementId { get; set; }

    /// <summary>Gets or sets the statement's number.</summary>
    public string StatementNumber { get; set; }

    /// <summary>Gets or sets the pay date.</summary>
    public DateOnly PayDate { get; set; }

    /// <summary>Gets or sets the amount withheld.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets how it was sent.</summary>
    public GarnishmentPaymentMethod Method { get; set; }

    /// <summary>Gets or sets the disbursement or check that sent it, or <see langword="null"/>.</summary>
    public string ReferenceId { get; set; }

    /// <summary>Gets or sets what people see of the payment, such as a check number.</summary>
    public string Reference { get; set; }

    /// <summary>Gets or sets why it could not be sent, or <see langword="null"/>.</summary>
    public string Problem { get; set; }

    /// <summary>Gets or sets whether the statement was voided (the withholding given back).</summary>
    public bool Voided { get; set; }

    /// <summary>Gets or sets how many times the payment to the payee was sent again after a problem.</summary>
    public int Attempt { get; set; }
}
