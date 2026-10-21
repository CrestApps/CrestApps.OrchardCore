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

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="IsLive"/> comes from the application configuration and
    /// cannot be changed on the page.
    /// </summary>
    [BindNever]
    public bool IsLiveFromConfiguration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the live publishable key comes from the application configuration.
    /// </summary>
    [BindNever]
    public bool LivePublishableKeyFromConfiguration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the live secret key comes from the application configuration.
    /// </summary>
    [BindNever]
    public bool LivePrivateSecretFromConfiguration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the live webhook signing secret comes from the application configuration.
    /// </summary>
    [BindNever]
    public bool LiveWebhookSecretFromConfiguration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test publishable key comes from the application configuration.
    /// </summary>
    [BindNever]
    public bool TestPublishableKeyFromConfiguration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test secret key comes from the application configuration.
    /// </summary>
    [BindNever]
    public bool TestPrivateSecretFromConfiguration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test webhook signing secret comes from the application configuration.
    /// </summary>
    [BindNever]
    public bool TestWebhookSecretFromConfiguration { get; set; }
}
