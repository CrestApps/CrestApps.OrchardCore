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
    /// The sales channel of the orders AI agents place.
    /// </summary>
    public const string OrderChannel = OrderChannels.Agent;
}
