using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.TimeZones.Services;

internal sealed class AdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Tools"], tools => tools
                .Add(S["Time Zones"], S["Time Zones"].PrefixPosition(), timeZones => timeZones
                    .AddClass("time-zones")
                    .Id("timeZones")
                    .Action("Index", "Admin", TimeZonesConstants.Features.Area)
                    .Permission(TimeZonesConstants.Permissions.ManageTimeZoneMaps)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
