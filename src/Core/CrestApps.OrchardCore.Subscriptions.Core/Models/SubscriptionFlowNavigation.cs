using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents navigation state for a subscription checkout flow.
/// </summary>
public class SubscriptionFlowNavigation
{
    /// <summary>
    /// Gets or sets the key of the previous step in the subscription flow.
    /// </summary>
    [BindNever]
    public string PreviousStep { get; set; }

    /// <summary>
    /// Gets or sets the key of the current step in the subscription flow.
    /// </summary>
    [BindNever]
    public string CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the key of the next step in the subscription flow.
    /// </summary>
    [BindNever]
    public string NextStep { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current step collects payment details.
    /// </summary>
    [BindNever]
    public bool IsPaymentStep { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the subscription session being navigated.
    /// </summary>
    [BindNever]
    public string SessionId { get; set; }
}
