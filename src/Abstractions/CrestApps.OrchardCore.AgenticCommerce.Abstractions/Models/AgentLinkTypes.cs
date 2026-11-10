namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// The kinds of links an agent checkout carries. Each protocol maps them to its own names.
/// </summary>
public static class AgentLinkTypes
{
    /// <summary>
    /// The terms of service.
    /// </summary>
    public const string TermsOfService = "terms_of_service";

    /// <summary>
    /// The privacy policy.
    /// </summary>
    public const string PrivacyPolicy = "privacy_policy";

    /// <summary>
    /// The return and refund policy.
    /// </summary>
    public const string ReturnPolicy = "return_policy";

    /// <summary>
    /// The shipping policy.
    /// </summary>
    public const string ShippingPolicy = "shipping_policy";

    /// <summary>
    /// The frequently asked questions.
    /// </summary>
    public const string Faq = "faq";

    /// <summary>
    /// The contact page.
    /// </summary>
    public const string Contact = "contact_us";

    /// <summary>
    /// The page about the store.
    /// </summary>
    public const string About = "about_us";
}
