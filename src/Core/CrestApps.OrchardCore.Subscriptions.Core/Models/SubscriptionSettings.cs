using System.ComponentModel;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents site-level settings for subscription checkout and billing.
/// </summary>
public sealed class SubscriptionSettings
{
    /// <summary>
    /// Gets or sets the key of the default payment method used by subscription checkout.
    /// </summary>
    public string DefaultPaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code used for subscription invoices and payments.
    /// </summary>
    [DefaultValue("USD")]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets a value indicating whether subscribers may complete checkout as guests.
    /// </summary>
    public bool AllowGuestSignup { get; set; }

    /// <summary>
    /// Gets or sets how many days a subscriber keeps access after a renewal payment fails.
    /// </summary>
    /// <remarks>
    /// A failed renewal is usually an expired card, not a customer who left. Cutting access off the same
    /// night loses subscribers who would have paid; never cutting it off gives the plan away. The grace
    /// window is where that judgment is made, so it is a setting rather than a constant.
    /// </remarks>
    [DefaultValue(7)]
    public int DunningGraceDays { get; set; } = 7;
}
