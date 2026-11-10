namespace CrestApps.OrchardCore.AgenticCommerce.Core.Models;

/// <summary>
/// How the store sells through AI agents (Settings › Commerce › Agentic commerce).
/// </summary>
public sealed class AgenticCommerceSettings
{
    /// <summary>
    /// Gets or sets how long an agent checkout lasts unless completed, in hours.
    /// </summary>
    public int CheckoutLifetimeHours { get; set; } = 6;

    /// <summary>
    /// Gets or sets how many open checkouts one agent platform may have at once.
    /// </summary>
    public int MaxOpenCheckoutsPerPlatform { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether a UCP platform the store doesn't know may buy, when its request is signed
    /// with a key from its own profile.
    /// </summary>
    public bool AllowUnregisteredUcpPlatforms { get; set; }

    /// <summary>
    /// Gets or sets the store's Stripe business profile ID (<c>profile_…</c>), which agents issue shared payment tokens
    /// to.
    /// </summary>
    public string StripeBusinessProfileId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Google Pay is offered to UCP agents.
    /// </summary>
    public bool EnableGooglePay { get; set; }

    /// <summary>
    /// Gets or sets the store's Google Pay merchant ID.
    /// </summary>
    public string GooglePayMerchantId { get; set; }

    /// <summary>
    /// Gets or sets the name Google Pay shows the buyer; the store's name when empty.
    /// </summary>
    public string GooglePayMerchantName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Google Pay runs in its test environment.
    /// </summary>
    public bool GooglePayTestEnvironment { get; set; } = true;
}
