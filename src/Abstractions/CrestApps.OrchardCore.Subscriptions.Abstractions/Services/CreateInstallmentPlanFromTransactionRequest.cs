using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// What turns an outstanding transaction into an installment plan while the customer pays it at checkout. The
/// amounts come from the transaction, never from the customer's browser; the installments come from the option the
/// customer chose, which an <see cref="IInstallmentOptionProvider"/> offered.
/// </summary>
public sealed class CreateInstallmentPlanFromTransactionRequest
{
    /// <summary>
    /// Gets or sets the outstanding transaction the plan replaces.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the checkout session the customer is paying the transaction in. It takes the plan's first payment
    /// and keeps the card for the rest.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the installments the customer chose.
    /// </summary>
    public InstallmentOption Option { get; set; }
}
