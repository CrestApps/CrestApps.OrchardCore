using System.ComponentModel;

namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// The site-level settings that govern checkout behavior across every checkout scenario.
/// </summary>
public sealed class CheckoutSettings
{
    /// <summary>
    /// The key of the payment provider selected by default when a checkout offers more than one.
    /// </summary>
    public string DefaultPaymentMethod { get; set; }

    /// <summary>
    /// The default ISO-4217 currency used when a checkout does not specify one.
    /// </summary>
    [DefaultValue("USD")]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets how long an unpaid checkout is kept before it expires and whatever the provider was
    /// holding for it is released. Defaults to one day.
    /// </summary>
    public int SessionLifetimeHours { get; set; } = 24;
}
