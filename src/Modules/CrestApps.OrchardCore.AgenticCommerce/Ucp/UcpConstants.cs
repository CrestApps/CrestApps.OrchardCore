namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// The names, addresses and headers of the Universal Commerce Protocol.
/// </summary>
public static class UcpConstants
{
    /// <summary>
    /// The path of the REST binding, after the tenant's base address.
    /// </summary>
    public const string RoutePrefix = "ucp/v1";

    /// <summary>
    /// The path of the store's profile.
    /// </summary>
    public const string ProfilePath = ".well-known/ucp";

    /// <summary>
    /// The header an agent names its platform's profile in.
    /// </summary>
    public const string AgentHeader = "UCP-Agent";

    /// <summary>
    /// The versions the store serves, newest first.
    /// </summary>
    public static readonly IReadOnlyList<string> Versions = [AgenticCommerceConstants.Versions.Ucp20260825, AgenticCommerceConstants.Versions.Ucp20260408];

    /// <summary>
    /// The capabilities and extensions of the shopping service.
    /// </summary>
    public static class Capabilities
    {
        /// <summary>
        /// Checkout sessions.
        /// </summary>
        public const string Checkout = "dev.ucp.shopping.checkout";

        /// <summary>
        /// Orders and order webhooks.
        /// </summary>
        public const string Order = "dev.ucp.shopping.order";

        /// <summary>
        /// The cart.
        /// </summary>
        public const string Cart = "dev.ucp.shopping.cart";

        /// <summary>
        /// Catalog search.
        /// </summary>
        public const string CatalogSearch = "dev.ucp.shopping.catalog.search";

        /// <summary>
        /// Catalog lookup.
        /// </summary>
        public const string CatalogLookup = "dev.ucp.shopping.catalog.lookup";

        /// <summary>
        /// The fulfillment extension.
        /// </summary>
        public const string Fulfillment = "dev.ucp.shopping.fulfillment";

        /// <summary>
        /// The discount extension.
        /// </summary>
        public const string Discount = "dev.ucp.shopping.discount";

        /// <summary>
        /// The buyer consent extension.
        /// </summary>
        public const string BuyerConsent = "dev.ucp.shopping.buyer_consent";

        /// <summary>
        /// Account linking.
        /// </summary>
        public const string IdentityLinking = "dev.ucp.common.identity_linking";

        /// <summary>
        /// The AP2 mandates extension in 2026-08-25.
        /// </summary>
        public const string Ap2Mandate = "dev.ucp.common.payment.ap2_mandate";

        /// <summary>
        /// The AP2 mandates extension in 2026-04-08.
        /// </summary>
        public const string Ap2MandateLegacy = "dev.ucp.shopping.ap2_mandate";
    }

    /// <summary>
    /// The Embedded Checkout Protocol (ECP): the store's checkout shown in the platform's page from <c>continue_url</c>.
    /// </summary>
    public static class Embedded
    {
        /// <summary>
        /// The name of the transport in the profile and in checkout answers.
        /// </summary>
        public const string Transport = "embedded";

        /// <summary>
        /// The schema of the transport, after a version's base address.
        /// </summary>
        public const string SchemaPath = "services/shopping/embedded.openrpc.json";

        /// <summary>
        /// The query parameter with the UCP version of the session; its presence asks for embedded checkout.
        /// </summary>
        public const string VersionParameter = "ec_version";

        /// <summary>
        /// The query parameter with the host's credential.
        /// </summary>
        public const string AuthParameter = "ec_auth";

        /// <summary>
        /// The query parameter with the delegations the host asks for.
        /// </summary>
        public const string DelegateParameter = "ec_delegate";

        /// <summary>
        /// The query parameter with the color scheme the host asks for: <c>light</c> or <c>dark</c>.
        /// </summary>
        public const string ColorSchemeParameter = "ec_color_scheme";

        /// <summary>
        /// The credential type the embedded checkout asks the host for: an OAuth access token from the store's OpenID
        /// server, issued to the platform's OAuth client.
        /// </summary>
        public const string AuthType = "oauth";

        /// <summary>
        /// The delegations the store accepts: none, so the buyer does every step in the store's own checkout. ECP
        /// requires no delegation.
        /// </summary>
        public static readonly IReadOnlyList<string> Delegations = [];
    }

    /// <summary>
    /// Returns the address of a page of a version's specification.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <param name="path">The page, such as <c>specification/shopping/checkout</c>.</param>
    public static string Url(string version, string path)
        => $"https://ucp.dev/{version}/{path}";
}
