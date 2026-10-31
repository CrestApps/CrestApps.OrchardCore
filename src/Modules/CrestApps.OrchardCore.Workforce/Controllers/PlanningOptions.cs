using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// Choices and dates shared by the shift planning screens.
/// </summary>
internal static class PlanningOptions
{
    /// <summary>
    /// Returns the teams the user manages, in tree order and indented by depth.
    /// </summary>
    public static async Task<IList<SelectListItem>> TeamsAsync(ITeamManager teamManager, WorkforceScope scope, string selected, CancellationToken cancellationToken)
    {
        return (await teamManager.ListTreeAsync(cancellationToken))
            .Where(item => scope.Covers(item.Team.ItemId))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, item.Team.ItemId == selected))
            .ToList();
    }

    /// <summary>
    /// Returns where a weekday sits in a week that starts on Monday.
    /// </summary>
    public static int DayIndex(DayOfWeek day)
        => ((int)day + 6) % 7;

    /// <summary>
    /// Returns the weekday at a place in a week that starts on Monday.
    /// </summary>
    public static DayOfWeek DayOf(int index)
        => (DayOfWeek)((index + 1) % 7);

    /// <summary>
    /// Returns the site's local date.
    /// </summary>
    public static async Task<DateOnly> TodayAsync(ILocalClock localClock)
        => DateOnly.FromDateTime((await localClock.GetLocalNowAsync()).DateTime);

    /// <summary>
    /// Returns the site's next Monday, or today when it is a Monday.
    /// </summary>
    public static async Task<DateOnly> NextMondayAsync(ILocalClock localClock)
    {
        var today = await TodayAsync(localClock);

        return today.AddDays((7 - DayIndex(today.DayOfWeek)) % 7);
    }
}
