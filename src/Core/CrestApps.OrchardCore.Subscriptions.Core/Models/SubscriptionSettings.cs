using System.ComponentModel;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class SubscriptionSettings
{
    [DefaultValue("USD")]
    public string Currency { get; set; } = "USD";

    public bool AllowGuestSignup { get; set; }
}
