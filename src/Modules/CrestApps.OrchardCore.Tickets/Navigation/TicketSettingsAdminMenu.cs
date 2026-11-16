using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tickets.Navigation;

/// <summary>
/// Adds Settings &gt; Service Desk, the service desk's site settings: the notification templates of the messages
/// requesters get and, with AI on, the AI profile for replies.
/// </summary>
internal sealed class TicketSettingsAdminMenu : AdminNavigationProvider
{
    /// <summary>
    /// The settings group of Settings &gt; Service Desk.
    /// </summary>
    public const string GroupId = "service-desk";

    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", GroupId },
    };

    internal readonly IStringLocalizer S;

    public TicketSettingsAdminMenu(IStringLocalizer<TicketSettingsAdminMenu> stringLocalizer)
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
