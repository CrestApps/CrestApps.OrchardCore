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
    /// Gets or sets a value indicating whether a UCP platform may show the store's checkout inside its own page (UCP
    /// embedded checkout), when the platform lists the origins that frame it.
    /// </summary>
    public bool AllowEmbeddedCheckout { get; set; }

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

    /// <summary>
    /// Gets or sets a value indicating whether an agent that can run 3-D Secure itself is asked to, when the buyer's
    /// bank wants to approve a payment, instead of handing the buyer to the store's checkout. It needs the acquirer
    /// details below and a payment provider that accepts an outside 3-D Secure result.
    /// </summary>
    public bool EnableDelegatedAuthentication { get; set; }

    /// <summary>
    /// Gets or sets the acquirer's BIN (up to 11 characters), from the payment provider.
    /// </summary>
    public string AcquirerBin { get; set; }

    /// <summary>
    /// Gets or sets the acquirer's country, as an ISO 3166-1 alpha-2 code.
    /// </summary>
    public string AcquirerCountry { get; set; }

    /// <summary>
    /// Gets or sets the store's merchant ID at the acquirer (up to 35 characters).
    /// </summary>
    public string AcquirerMerchantId { get; set; }

    /// <summary>
    /// Gets or sets the merchant name the buyer's bank shows during 3-D Secure (up to 40 characters).
    /// </summary>
    public string AuthenticationMerchantName { get; set; }

    /// <summary>
    /// Gets or sets the 3-D Secure requestor ID (up to 35 characters), when the store has one.
    /// </summary>
    public string ThreeDSecureRequestorId { get; set; }
}
