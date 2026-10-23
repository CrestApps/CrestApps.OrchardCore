using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The outcome of an <see cref="ITransactionCommandService"/> command.
/// </summary>
public sealed class TransactionCommandResult
{
    /// <summary>
    /// Gets a value indicating whether the command succeeded, meaning no error was reported.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the transaction as it stands after the command, or <see langword="null"/> when no transaction has
    /// the identifier the command was given.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the timeline event the command recorded, such as the recorded payment or the adjustment, when it
    /// recorded one.
    /// </summary>
    public TransactionEvent Event { get; set; }

    /// <summary>
    /// Gets the refunds <see cref="ITransactionCommandService.RefundAsync"/> requested, newest payment first, with the
    /// status each one reached.
    /// </summary>
    public IList<PaymentRefund> Refunds { get; } = [];

    /// <summary>
    /// Gets or sets how a charge of a saved payment method ended, for
    /// <see cref="ITransactionCommandService.ChargeSavedPaymentMethodAsync"/>. Every other command leaves it
    /// <see cref="SavedPaymentMethodChargeStatus.NotCharged"/>.
    /// </summary>
    public SavedPaymentMethodChargeStatus ChargeStatus { get; set; }

    /// <summary>
    /// Gets the reasons the command was refused or did not fully succeed, ready to show to the person who asked.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Adds an error to the result.
    /// </summary>
    /// <param name="message">The message to show.</param>
    /// <returns>The same result, so calls can be chained.</returns>
    public TransactionCommandResult Fail(string message)
    {
        Errors.Add(message);

        return this;
    }
}
