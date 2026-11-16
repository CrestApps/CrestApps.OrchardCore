using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// Adds Settings &gt; Service Desk, with the service desk's AI settings. Registered only with AI on.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
internal sealed class TicketAISettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", TicketAISettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public TicketAISettingsAdminMenu(IStringLocalizer<TicketAISettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Settings"], settings => settings
            .Add(S["Service Desk"], S["Service Desk"].PrefixPosition(), desk => desk
                .Id("serviceDeskSettings")
                .Action("Index", "Admin", _routeValues)
                .Permission(TicketsPermissions.ManageTicketSettings)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
