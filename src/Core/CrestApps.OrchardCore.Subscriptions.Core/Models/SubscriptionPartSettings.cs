namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class SubscriptionPartSettings
{
    public bool AllowGuestSignup { get; set; }

    /// <summary>
    /// When provides, the Subscription Flow will add a step for each content Types to be created.
    /// </summary>
    public string[] ContentTypes { get; set; }
}
