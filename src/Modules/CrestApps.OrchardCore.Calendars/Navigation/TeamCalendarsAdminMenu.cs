using CrestApps.OrchardCore.Calendars.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Calendars.Navigation;

/// <summary>
/// Adds Team availability to the Calendars menu.
/// </summary>
public sealed class TeamCalendarsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", CalendarsConstants.Features.Calendars },
        { "controller", typeof(TeamAvailabilityController).ControllerName() },
        { "action", nameof(TeamAvailabilityController.Index) },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamCalendarsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TeamCalendarsAdminMenu(IStringLocalizer<TeamCalendarsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Calendars"], calendars => calendars
            .Add(S["Team Availability"], S["Team Availability"].PrefixPosition("3"), availability => availability
                .Id("teamAvailability")
                .Action(_routeValues)
                .Permission(CalendarPermissions.AccessCalendars)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
