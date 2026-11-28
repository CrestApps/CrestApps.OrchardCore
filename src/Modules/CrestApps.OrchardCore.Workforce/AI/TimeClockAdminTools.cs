using System.Security.Claims;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Timesheet lookups shared by the time clock administration AI tools: a timesheet in the teams the user manages, and
/// the time zone its punches are entered in.
/// </summary>
internal static class TimeClockAdminTools
{
    /// <summary>
    /// Finds a timesheet by its ID and checks the user manages its team, as the Timesheets pages do.
    /// </summary>
    public static async Task<(Timesheet Timesheet, string Error)> FindTimesheetAsync(IServiceProvider services, ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return (null, "Give the timesheet's ID, from listTimesheets or getTimesheet.");
        }

        var timesheet = await services.GetRequiredService<ITimesheetService>().FindAsync(id, cancellationToken);

        if (timesheet is null)
        {
            return (null, $"No timesheet has the ID '{id}'.");
        }

        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        if (!scope.Covers(timesheet.TeamId))
        {
            return (null, "The user doesn't manage this timesheet's team.");
        }

        return (timesheet, null);
    }

    /// <summary>
    /// Returns the time zone a timesheet's days and punches are in (the worker's), as the timesheet page uses it.
    /// </summary>
    public static async Task<string> ZoneOfAsync(IServiceProvider services, Timesheet timesheet, CancellationToken cancellationToken)
    {
        var (_, calculation) = await services.GetRequiredService<ITimesheetService>().CalculateAsync(timesheet.ItemId, cancellationToken);

        return calculation?.TimeZoneId ?? "UTC";
    }

    /// <summary>
    /// The statuses a timesheet can have, for the tools' messages.
    /// </summary>
    public static string StatusList
        => string.Join(", ", Enum.GetNames<TimesheetStatus>());
}
