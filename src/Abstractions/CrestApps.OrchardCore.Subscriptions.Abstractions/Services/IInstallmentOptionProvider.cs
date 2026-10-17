using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Offers the installments a customer may choose when they pay an outstanding transaction at checkout. The module that
/// creates a kind of transaction implements it for its own <see cref="Transaction.Source"/>, so it decides which of its
/// balances may be split and how, for example a club offering "3 monthly payments" for a season fee.
/// </summary>
/// <remarks>
/// Options are only asked for when the transaction can be turned into a plan at all: it is owed in full by a signed-up
/// customer, it is not already a plan payment or a recurring charge, and a payment provider that can keep a card is
/// enabled. The checkout re-reads the options when the customer submits their choice, so an option that is withdrawn in
/// the meantime cannot be chosen.
/// </remarks>
public interface IInstallmentOptionProvider
{
    /// <summary>
    /// Gets the transaction source this provider offers options for, matched against <see cref="Transaction.Source"/>
    /// ignoring case. <see langword="null"/> or empty asks the provider about every transaction, whatever its source.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Returns the options offered for the transaction, or none to offer only paying in full.
    /// </summary>
    /// <param name="transaction">The outstanding transaction the customer is paying.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IEnumerable<InstallmentOption>> GetOptionsAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
