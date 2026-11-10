using CrestApps.OrchardCore.AgenticCommerce.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AgenticCommerce.Navigation;

/// <summary>
/// Adds Commerce › AI agents and Settings › Commerce › Agentic commerce to the admin menu.
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
                .Add(S["AI agents"], S["AI agents"].PrefixPosition("10"), agents => agents
                    .Id("agentPlatforms")
                    .Action(_platformsRouteValues)
                    .Permission(AgenticCommercePermissions.ManageAgentPlatforms)
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Agentic commerce"], S["Agentic commerce"].PrefixPosition(), agentic => agentic
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
