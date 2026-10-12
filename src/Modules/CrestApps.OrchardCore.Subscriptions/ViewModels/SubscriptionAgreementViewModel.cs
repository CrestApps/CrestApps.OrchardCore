using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// One agreement on the administration screen, with its owner named.
/// </summary>
public class SubscriptionAgreementViewModel
{
    /// <summary>
    /// Gets or sets the agreement.
    /// </summary>
    public Subscription Subscription { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who owns it, or <see langword="null"/> when they can't be found.
    /// </summary>
    public string OwnerName { get; set; }
}
