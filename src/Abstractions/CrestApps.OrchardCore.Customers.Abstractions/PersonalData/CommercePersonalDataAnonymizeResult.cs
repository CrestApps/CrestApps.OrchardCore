namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// How many records an anonymization changed, by kind, and what it could not do.
/// </summary>
public sealed class CommercePersonalDataAnonymizeResult
{
    /// <summary>
    /// Gets or sets the number of transactions anonymized.
    /// </summary>
    public int Transactions { get; set; }

    /// <summary>
    /// Gets or sets the number of payments whose free text named the person and was scrubbed.
    /// </summary>
    public int Payments { get; set; }

    /// <summary>
    /// Gets or sets the number of refunds whose free text named the person and was scrubbed.
    /// </summary>
    public int Refunds { get; set; }

    /// <summary>
    /// Gets or sets the number of installment plans anonymized.
    /// </summary>
    public int InstallmentPlans { get; set; }

    /// <summary>
    /// Gets or sets the number of subscriptions anonymized.
    /// </summary>
    public int Subscriptions { get; set; }

    /// <summary>
    /// Gets or sets the number of checkout sessions anonymized.
    /// </summary>
    public int CheckoutSessions { get; set; }

    /// <summary>
    /// Gets or sets the number of coupon redemptions anonymized.
    /// </summary>
    public int CouponRedemptions { get; set; }

    /// <summary>
    /// Gets or sets the number of payment-provider customers redacted.
    /// </summary>
    public int ProviderCustomers { get; set; }

    /// <summary>
    /// Gets or sets how many back-in-stock requests were deleted.
    /// </summary>
    public int StockAlerts { get; set; }

    /// <summary>
    /// Gets what could not be anonymized, such as a payment provider that could not be reached.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];
}
