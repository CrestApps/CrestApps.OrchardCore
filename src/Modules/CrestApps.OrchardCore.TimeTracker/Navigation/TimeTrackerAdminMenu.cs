using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.TimeTracker.Navigation;

/// <summary>
/// Adds Time Tracker to the admin menu.
/// </summary>
public sealed class TimeTrackerAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeTrackerAdminMenu"/> class.
    /// </summary>
    public TimeTrackerAdminMenu(IStringLocalizer<TimeTrackerAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Time Tracker"], S["Time Tracker"].PrefixPosition("8"), tracker => tracker
            .AddClass("time-tracker")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-stopwatch")
            .Id("timeTracker")
            .Add(S["My Time"], S["My Time"].PrefixPosition("10"), mine => mine
                .Id("timeTrackerMine")
                .Action("Index", "MyTime", TimeTrackerConstants.Features.TimeTracker)
                .Permission(TimeTrackerPermissions.TrackOwnTime)
                .LocalNav())
            .Add(S["Time Entries"], S["Time Entries"].PrefixPosition("20"), entries => entries
                .Id("timeTrackerEntries")
                .Action("Index", "TimeEntries", TimeTrackerConstants.Features.TimeTracker)
                .Permission(TimeTrackerPermissions.ViewTimeEntries)
                .LocalNav())
            .Add(S["Stages"], S["Stages"].PrefixPosition("60"), stages => stages
                .Id("timeTrackerStages")
                .Action("Index", "TimeSettings", new RouteValueDictionary { { "area", TimeTrackerConstants.Features.TimeTracker }, { "kind", "stages" } })
                .Permission(TimeTrackerPermissions.ManageTimeTrackerSettings)
                .LocalNav())
            .Add(S["Categories"], S["Categories"].PrefixPosition("61"), categories => categories
                .Id("timeTrackerCategories")
                .Action("Index", "TimeSettings", new RouteValueDictionary { { "area", TimeTrackerConstants.Features.TimeTracker }, { "kind", "categories" } })
                .Permission(TimeTrackerPermissions.ManageTimeTrackerSettings)
                .LocalNav()));

        // The hours by category report is listed by the Reports module, under Reports > Workforce & Payroll.
        return ValueTask.CompletedTask;
    }
}
