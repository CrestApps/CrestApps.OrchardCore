using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Pays a payment of net pay when a run is processed: Direct Deposit pays a deposit through Disbursements, Paychecks
/// prints a check through Checks. A feature registers one per <see cref="NetPayMethod"/> it pays; with none for a
/// method, the payment waits and the run is only partly paid.
/// </summary>
public interface IPayStatementPaymentMethod
{
    /// <summary>
    /// Gets the method it pays.
    /// </summary>
    NetPayMethod Method { get; }

    /// <summary>
    /// Pays a payment: creates the disbursement or the check, once (a second call for the same payment finds it).
    /// </summary>
    /// <param name="context">The statement, the payment and who is paid.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reference of what pays it, or the errors.</returns>
    Task<PayStatementPaymentResult> PayAsync(PayStatementPaymentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a payment when its statement is voided: voids the check, or cancels a deposit not sent to the bank yet.
    /// </summary>
    /// <param name="context">The statement and the payment.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it was stopped; <see langword="false"/> when it already went out.</returns>
    Task<bool> VoidAsync(PayStatementPaymentContext context, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// A payment to pay.
/// </summary>
public sealed class PayStatementPaymentContext
{
    /// <summary>Gets or sets the statement.</summary>
    public PayStatement Statement { get; set; }

    /// <summary>Gets or sets the payment.</summary>
    public PayStatementPayment Payment { get; set; }

    /// <summary>Gets or sets the payment's place among the statement's payments, for its idempotency key.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the pay profile.</summary>
    public EmployeePayProfile Profile { get; set; }

    /// <summary>Gets or sets the employer.</summary>
    public Employer Employer { get; set; }
}

/// <summary>
/// The result of paying a payment.
/// </summary>
public sealed class PayStatementPaymentResult
{
    /// <summary>Gets or sets the reference of the disbursement or check that pays it.</summary>
    public string ReferenceId { get; set; }

    /// <summary>Gets or sets a short text for the statement, such as a check number.</summary>
    public string Reference { get; set; }

    /// <summary>Gets the errors.</summary>
    public IList<string> Errors { get; } = [];

    /// <summary>Gets a value indicating whether it was paid.</summary>
    public bool Succeeded
        => Errors.Count == 0 && !string.IsNullOrEmpty(ReferenceId);
}

/// <summary>
/// Hears that a statement was issued (made final) or voided, such as to send the person a notice.
/// </summary>
public interface IPayStatementObserver
{
    /// <summary>
    /// Called once a statement is final and its payments were made.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task IssuedAsync(PayStatement statement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called once a statement is voided.
    /// </summary>
    /// <param name="statement">The voided statement.</param>
    /// <param name="reversal">The reversing statement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task VoidedAsync(PayStatement statement, PayStatement reversal, CancellationToken cancellationToken = default);
}
