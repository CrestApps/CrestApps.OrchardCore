using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// The permissions of agent product feeds.
/// </summary>
public static class AgentFeedPermissions
{
    /// <summary>
    /// Sets up the product feeds AI agents read and the pushes to agent platforms.
    /// </summary>
    public static readonly Permission ManageAgentFeeds = new(
        "ManageAgentFeeds",
        LocalizationSource.Create("Manage agent product feeds", typeof(AgentFeedPermissions)));
}
