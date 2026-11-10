using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core;

/// <summary>
/// Where agentic commerce keeps its records.
/// </summary>
public static class AgenticCommerceStorage
{
    /// <summary>
    /// The YesSql collection agent platforms and agent checkouts are kept in.
    /// </summary>
    public const string CollectionName = "AgenticCommerce";

    /// <summary>
    /// The purpose that protects agent platforms' webhook secrets.
    /// </summary>
    public const string SecretProtectionPurpose = "CrestApps.OrchardCore.AgenticCommerce.Secrets";

    /// <summary>
    /// The purpose of the signed token that hands an agent checkout to the buyer's browser.
    /// </summary>
    public const string HandoffTokenPurpose = "CrestApps.OrchardCore.AgenticCommerce.Handoff";

    /// <summary>
    /// The purpose of the signed token an embedded checkout reads its checkout and checks its host's credential with.
    /// </summary>
    public const string EmbeddedCheckoutTokenPurpose = "CrestApps.OrchardCore.AgenticCommerce.EmbeddedCheckout";

    /// <summary>
    /// The purpose of the cookie that marks the browser frame an embedded checkout runs in.
    /// </summary>
    public const string EmbeddedCheckoutContextPurpose = "CrestApps.OrchardCore.AgenticCommerce.EmbeddedCheckoutContext";

    /// <summary>
    /// The name of the cookie that marks the browser frame an embedded checkout runs in.
    /// </summary>
    public const string EmbeddedCheckoutContextCookieName = "crestapps_agent_ec";

    /// <summary>
    /// The sales channel of the orders AI agents place.
    /// </summary>
    public const string OrderChannel = OrderChannels.Agent;
}
