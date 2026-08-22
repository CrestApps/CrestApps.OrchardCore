namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the result returned after a Stripe SetupIntent is created.
/// </summary>
public class CreateSetupIntentResponse
{
    /// <summary>
    /// Gets or sets the Stripe setup intent identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the current Stripe status of the setup intent.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the client secret used by Stripe.js to confirm the setup intent in the browser.
    /// </summary>
    public string ClientSecret { get; set; }
}
