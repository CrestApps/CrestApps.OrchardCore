using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the payment method selection step in a subscription flow.
/// </summary>
public class PaymentMethodsViewModel
{
    /// <summary>
    /// Gets or sets the selected payment method key.
    /// </summary>
    public string PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets the available payment method options.
    /// </summary>
    [BindNever]
    public PaymentMethodOptionViewModel[] PaymentMethods { get; set; }

    /// <summary>
    /// Gets the subscription flow associated with the payment method step.
    /// </summary>
    [BindNever]
    public SubscriptionFlow Flow { get; internal set; }
}
