namespace CrestApps.OrchardCore.Commerce.Stores;

/// <summary>
/// The links to the store's policies. Each is an absolute URL or a path on the site, such as <c>/terms</c>.
/// </summary>
public sealed class StorePolicyLinks
{
    /// <summary>
    /// Gets or sets the link to the terms of service.
    /// </summary>
    public string TermsOfService { get; set; }

    /// <summary>
    /// Gets or sets the link to the privacy policy.
    /// </summary>
    public string PrivacyPolicy { get; set; }

    /// <summary>
    /// Gets or sets the link to the return and refund policy.
    /// </summary>
    public string ReturnPolicy { get; set; }

    /// <summary>
    /// Gets or sets the link to the shipping policy.
    /// </summary>
    public string ShippingPolicy { get; set; }

    /// <summary>
    /// Gets or sets the link to the frequently asked questions.
    /// </summary>
    public string Faq { get; set; }

    /// <summary>
    /// Gets or sets the link to the page buyers contact the store from.
    /// </summary>
    public string Contact { get; set; }

    /// <summary>
    /// Gets or sets the link to the page about the store.
    /// </summary>
    public string About { get; set; }
}
