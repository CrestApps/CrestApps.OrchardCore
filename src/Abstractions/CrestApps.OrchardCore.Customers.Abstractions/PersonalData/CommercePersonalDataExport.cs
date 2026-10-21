using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// Everything the enabled commerce modules hold about one person, for an access request.
/// </summary>
public sealed class CommercePersonalDataExport
{
    /// <summary>
    /// Gets or sets the owner the request named, or <see langword="null"/>.
    /// </summary>
    public CustomerOwner Owner { get; set; }

    /// <summary>
    /// Gets or sets the email the request named, or <see langword="null"/>.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets when the export was made.
    /// </summary>
    public DateTime ExportedUtc { get; set; }

    /// <summary>
    /// Gets the person's ledger transactions: what they owed and paid, with invoice and receipt numbers.
    /// </summary>
    public IList<CommerceTransactionRecord> Transactions { get; init; } = [];

    /// <summary>
    /// Gets the payments taken from the person.
    /// </summary>
    public IList<CommercePaymentRecord> Payments { get; init; } = [];

    /// <summary>
    /// Gets the refunds given back to the person.
    /// </summary>
    public IList<CommerceRefundRecord> Refunds { get; init; } = [];

    /// <summary>
    /// Gets the person's installment plans.
    /// </summary>
    public IList<CommerceInstallmentPlanRecord> InstallmentPlans { get; init; } = [];

    /// <summary>
    /// Gets the person's subscriptions.
    /// </summary>
    public IList<CommerceSubscriptionRecord> Subscriptions { get; init; } = [];

    /// <summary>
    /// Gets the person's checkout sessions, with what each one recorded about them.
    /// </summary>
    public IList<CommerceCheckoutSessionRecord> CheckoutSessions { get; init; } = [];

    /// <summary>
    /// Gets the coupons the person redeemed.
    /// </summary>
    public IList<CommerceCouponRedemptionRecord> CouponRedemptions { get; init; } = [];

    /// <summary>
    /// Gets the customers payment providers keep for the person.
    /// </summary>
    public IList<CommerceProviderCustomerRecord> ProviderCustomers { get; init; } = [];

    /// <summary>
    /// Gets the person's requests to hear when sold-out products were back.
    /// </summary>
    public IList<CommerceStockAlertRecord> StockAlerts { get; init; } = [];

    /// <summary>
    /// Gets what could not be exported, such as a payment provider that could not be reached.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];
}
