using CrestApps.OrchardCore.Bookings.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Bookings.Navigation;

/// <summary>
/// Commerce › Bookings.
/// </summary>
public sealed class BookingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _bookings = new()
    {
        { "area", BookingsConstants.Feature },
        { "controller", typeof(BookingsAdminController).ControllerName() },
        { "action", nameof(BookingsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public BookingsAdminMenu(IStringLocalizer<BookingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Bookings"], S["Bookings"].PrefixPosition("2.5"), bookings => bookings
                    .AddClass("commerce-bookings")
                    .Id("commerceBookings")
                    .Action(_bookings)
                    .Permission(BookingsPermissions.ManageBookings)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
