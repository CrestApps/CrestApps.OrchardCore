namespace CrestApps.OrchardCore.AgenticCommerce.ViewModels;

/// <summary>
/// The page an agent platform frames for UCP embedded checkout.
/// </summary>
public class EmbeddedCheckoutViewModel
{
    /// <summary>
    /// Gets or sets the page's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the store checkout page the frame starts at.
    /// </summary>
    public string CheckoutUrl { get; set; }

    /// <summary>
    /// Gets or sets the color scheme the platform asked for: <c>light</c>, <c>dark</c> or <see langword="null"/>.
    /// </summary>
    public string ColorScheme { get; set; }

    /// <summary>
    /// Gets or sets the script's settings, as JSON.
    /// </summary>
    public string Config { get; set; }
}
