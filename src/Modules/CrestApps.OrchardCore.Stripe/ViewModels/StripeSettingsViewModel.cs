using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Stripe.ViewModels;

/// <summary>
/// Represents the editable Stripe site settings shown in the admin UI.
/// </summary>
public class StripeSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether Stripe live mode is enabled.
    /// </summary>
    public bool IsLive { get; set; }

    /// <summary>
    /// Gets or sets the Stripe checkout mode used by the site.
    /// </summary>
    public StripeCheckoutMode CheckoutMode { get; set; }

    /// <summary>
    /// Gets or sets the live publishable API key.
    /// </summary>
    public string LivePublishableKey { get; set; }

    /// <summary>
    /// Gets or sets the live private API secret submitted by the administrator.
    /// </summary>
    public string LivePrivateSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a live private secret is already stored.
    /// </summary>
    [BindNever]
    public bool HasLivePrivateSecret { get; set; }

    /// <summary>
    /// Gets or sets the live webhook signing secret submitted by the administrator.
    /// </summary>
    public string LiveWebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a live webhook secret is already stored.
    /// </summary>
    [BindNever]
    public bool HasLiveWebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the verified live Stripe account, when connected.
    /// </summary>
    [BindNever]
    public string LiveAccountId { get; set; }

    /// <summary>
    /// Gets or sets the test publishable API key.
    /// </summary>
    public string TestPublishableKey { get; set; }

    /// <summary>
    /// Gets or sets the test private API secret submitted by the administrator.
    /// </summary>
    public string TestPrivateSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a test private secret is already stored.
    /// </summary>
    [BindNever]
    public bool HasTestPrivateSecret { get; set; }

    /// <summary>
    /// Gets or sets the test webhook signing secret submitted by the administrator.
    /// </summary>
    public string TestWebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a test webhook secret is already stored.
    /// </summary>
    [BindNever]
    public bool HasTestWebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the verified test Stripe account, when connected.
    /// </summary>
    [BindNever]
    public string TestAccountId { get; set; }
}
