namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the subscription currency settings displayed in the admin settings UI.
/// </summary>
public class CurrencySubscriptionSettingsViewModel
{
    /// <summary>
    /// Gets or sets the ISO currency code currently used for subscription prices.
    /// </summary>
    public string CurrentCurrency { get; set; }
}
