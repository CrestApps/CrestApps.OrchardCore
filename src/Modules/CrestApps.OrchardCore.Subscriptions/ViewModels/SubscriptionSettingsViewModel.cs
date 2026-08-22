using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents site-level subscription checkout settings.
/// </summary>
public class SubscriptionSettingsViewModel
{
    /// <summary>
    /// Gets or sets the key of the default payment method used by subscription checkout.
    /// </summary>
    public string DefaultPaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether subscribers may complete checkout as guests.
    /// </summary>
    public bool AllowGuestSignup { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code used for subscription invoices and payments.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the currencies available for subscription checkout.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Currencies { get; set; }

    /// <summary>
    /// Gets or sets the payment methods available for subscription checkout.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> PaymentMethods { get; set; }
}
