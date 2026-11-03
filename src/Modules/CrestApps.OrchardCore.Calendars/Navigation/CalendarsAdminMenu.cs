using CrestApps.OrchardCore.Calendars.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Calendars.Navigation;

/// <summary>
/// Adds the Calendars menu: the calendar view and the list of calendars.
/// </summary>
public sealed class CalendarsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _calendarRoute = new()
    {
        { "area", CalendarsConstants.Features.Calendars },
        { "controller", typeof(CalendarController).ControllerName() },
        { "action", nameof(CalendarController.Index) },
    };

    private static readonly RouteValueDictionary _calendarsRoute = new()
    {
        { "area", CalendarsConstants.Features.Calendars },
        { "controller", typeof(CalendarsController).ControllerName() },
        { "action", nameof(CalendarsController.Index) },
    };

    private static readonly RouteValueDictionary _categoriesRoute = new()
    {
        { "area", CalendarsConstants.Features.Calendars },
        { "controller", typeof(CalendarCategoriesController).ControllerName() },
        { "action", nameof(CalendarCategoriesController.Index) },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarsAdminMenu(IStringLocalizer<CalendarsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Calendars"], "after.10", calendars => calendars
            .AddClass("calendars")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-calendar-days")
            .Id("calendars")
            .Add(S["Calendar"], S["Calendar"].PrefixPosition("1"), calendar => calendar
                .Id("calendarView")
                .Action(_calendarRoute)
                .Permission(CalendarPermissions.AccessCalendars)
                .LocalNav())
            .Add(S["Manage Calendars"], S["Manage Calendars"].PrefixPosition("2"), list => list
                .Id("calendarList")
                .Action(_calendarsRoute)
                .Permission(CalendarPermissions.AccessCalendars)
                .LocalNav())
            .Add(S["Categories"], S["Categories"].PrefixPosition("3"), categories => categories
                .Id("calendarCategories")
                .Action(_categoriesRoute)
                .Permission(CalendarPermissions.AccessCalendars)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
