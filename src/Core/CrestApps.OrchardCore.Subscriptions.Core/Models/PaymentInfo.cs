using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// This class is used in session to track payment info.
/// </summary>
public class PaymentInfo
{
    public PaymentStatus Status { get; set; }

    public double Amount { get; set; }

    /// <summary>
    /// The tax portion of <see cref="Amount"/> for this transaction, determined by the taxation
    /// framework at the time the transaction was created. Zero when taxation is disabled.
    /// </summary>
    public double TaxAmount { get; set; }

    /// <summary>
    /// The immutable tax determination captured when this transaction was created. Recurring charges
    /// each carry their own snapshot so historical tax is never recalculated with current rules.
    /// </summary>
    public TaxSnapshot TaxSnapshot { get; set; }

    public string Currency { get; set; }

    public string SubscriptionId { get; set; }

    public string GatewayId { get; set; }

    public GatewayMode GatewayMode { get; set; }

    public string TransactionId { get; set; }
}

public enum PaymentStatus
{
    Unknown,
    Succeeded,
    Failed,
}
