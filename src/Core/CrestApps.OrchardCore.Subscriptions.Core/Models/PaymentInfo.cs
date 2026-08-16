using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Stores payment information for a subscription transaction in the session.
/// </summary>
public class PaymentInfo
{
    /// <summary>
    /// Gets or sets the current payment status.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the payment amount.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax portion of <see cref="Amount"/> for this transaction, determined by the taxation
    /// framework at the time the transaction was created. Zero when taxation is disabled.
    /// </summary>
    public double TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the immutable tax determination captured when this transaction was created. Recurring charges
    /// each carry their own snapshot so historical tax is never recalculated with current rules.
    /// </summary>
    public TaxSnapshot TaxSnapshot { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code used for the payment.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the gateway subscription identifier related to the payment.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the payment gateway identifier.
    /// </summary>
    public string GatewayId { get; set; }

    /// <summary>
    /// Gets or sets the environment mode used by the payment gateway.
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// Gets or sets the local or gateway transaction identifier.
    /// </summary>
    public string TransactionId { get; set; }
}

/// <summary>
/// Defines the outcome state of a subscription payment.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// The payment status is not known.
    /// </summary>
    Unknown,

    /// <summary>
    /// The payment succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The payment failed.
    /// </summary>
    Failed,
}
