namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents one selectable payment method in the subscription checkout UI.
/// </summary>
public class PaymentMethodOptionViewModel
{
    /// <summary>
    /// Gets or sets the payment method key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the payment method title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the short description shown below the title.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this method is the configured default.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this method uses an online processor.
    /// </summary>
    public bool HasProcessor { get; set; }
}
