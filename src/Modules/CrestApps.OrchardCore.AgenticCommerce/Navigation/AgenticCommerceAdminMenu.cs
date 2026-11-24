using CrestApps.OrchardCore.AgenticCommerce.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AgenticCommerce.Navigation;

/// <summary>
/// Adds Commerce › AI Agents and Settings › Commerce › Agentic Commerce to the admin menu.
/// </summary>
public sealed class AgenticCommerceAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _platformsRouteValues = new()
    {
        { "area", AgenticCommerceConstants.Features.AgenticCommerce },
        { "controller", "AgentPlatforms" },
        { "action", "Index" },
    };

    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", AgenticCommerceSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgenticCommerceAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgenticCommerceAdminMenu(IStringLocalizer<AgenticCommerceAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["AI Agents"], S["AI Agents"].PrefixPosition("10"), agents => agents
                    .Id("agentPlatforms")
                    .Action(_platformsRouteValues)
                    .Permission(AgenticCommercePermissions.ManageAgentPlatforms)
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Agentic Commerce"], S["Agentic Commerce"].PrefixPosition(), agentic => agentic
                        .Id("agenticCommerceSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(AgenticCommercePermissions.ManageAgenticCommerceSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
