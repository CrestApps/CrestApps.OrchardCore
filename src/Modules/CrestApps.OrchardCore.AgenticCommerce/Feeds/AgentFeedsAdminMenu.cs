using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Settings › Commerce › Agent feeds.
/// </summary>
internal sealed class AgentFeedsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AgentFeedsAdminMenu(IStringLocalizer<AgentFeedsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Agent feeds"], S["Agent feeds"].PrefixPosition(), feeds => feeds
                        .Id("agentFeedsSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = AgentFeedSettingsDisplayDriver.GroupId })
                        .Permission(AgentFeedPermissions.ManageAgentFeeds)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
