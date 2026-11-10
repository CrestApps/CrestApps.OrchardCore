using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// The permissions of agentic commerce.
/// </summary>
public static class AgenticCommercePermissions
{
    /// <summary>
    /// Lets a user add, change and remove the AI agent platforms that may buy from the store.
    /// </summary>
    public static readonly Permission ManageAgentPlatforms = new(
        "ManageAgentPlatforms",
        LocalizationSource.Create("Manage AI agent platforms", typeof(AgenticCommercePermissions)));

    /// <summary>
    /// Lets a user change how the store sells through AI agents.
    /// </summary>
    public static readonly Permission ManageAgenticCommerceSettings = new(
        "ManageAgenticCommerceSettings",
        LocalizationSource.Create("Manage agentic commerce settings", typeof(AgenticCommercePermissions)));
}
