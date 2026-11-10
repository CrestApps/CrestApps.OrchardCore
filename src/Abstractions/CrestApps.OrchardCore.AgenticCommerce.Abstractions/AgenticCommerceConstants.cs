namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// The feature IDs, protocol names and protocol versions of agentic commerce.
/// </summary>
public static class AgenticCommerceConstants
{
    /// <summary>
    /// The feature IDs of the Agentic Commerce module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Agent platforms, the merchant's signing keys and the protocol-neutral agent checkout.
        /// </summary>
        public const string AgenticCommerce = "CrestApps.OrchardCore.AgenticCommerce";

        /// <summary>
        /// The Universal Commerce Protocol: discovery, checkout, fulfillment, discounts, orders and order webhooks.
        /// </summary>
        public const string Ucp = "CrestApps.OrchardCore.AgenticCommerce.Ucp";

        /// <summary>
        /// UCP account linking through the tenant's OpenID server.
        /// </summary>
        public const string UcpIdentityLinking = "CrestApps.OrchardCore.AgenticCommerce.Ucp.IdentityLinking";

        /// <summary>
        /// UCP catalog search and lookup, and the UCP cart.
        /// </summary>
        public const string UcpCatalog = "CrestApps.OrchardCore.AgenticCommerce.Ucp.Catalog";

        /// <summary>
        /// UCP's MCP binding.
        /// </summary>
        public const string UcpMcp = "CrestApps.OrchardCore.AgenticCommerce.Ucp.Mcp";

        /// <summary>
        /// The Agentic Commerce Protocol: checkout, discovery and order webhooks.
        /// </summary>
        public const string Acp = "CrestApps.OrchardCore.AgenticCommerce.Acp";

        /// <summary>
        /// Product feeds for AI agents: OpenAI's file feed and the ACP feed API client.
        /// </summary>
        public const string Feeds = "CrestApps.OrchardCore.AgenticCommerce.Feeds";

        /// <summary>
        /// The Agent Payments Protocol mandates extension of UCP (preview).
        /// </summary>
        public const string Ap2 = "CrestApps.OrchardCore.AgenticCommerce.Ap2";
    }

    /// <summary>
    /// The protocols an agent checkout is made through.
    /// </summary>
    public static class Protocols
    {
        /// <summary>
        /// The Universal Commerce Protocol.
        /// </summary>
        public const string Ucp = "ucp";

        /// <summary>
        /// The Agentic Commerce Protocol.
        /// </summary>
        public const string Acp = "acp";

        /// <summary>
        /// The site's own AI assistant, through its commerce tools.
        /// </summary>
        public const string Assistant = "assistant";
    }

    /// <summary>
    /// The protocol versions the store serves.
    /// </summary>
    public static class Versions
    {
        /// <summary>
        /// The latest UCP release.
        /// </summary>
        public const string Ucp20260825 = "2026-08-25";

        /// <summary>
        /// The UCP release Google's native checkout targets.
        /// </summary>
        public const string Ucp20260408 = "2026-04-08";

        /// <summary>
        /// The latest ACP release.
        /// </summary>
        public const string Acp20260417 = "2026-04-17";

        /// <summary>
        /// The ACP release that introduced payment handlers.
        /// </summary>
        public const string Acp20260130 = "2026-01-30";
    }
}
