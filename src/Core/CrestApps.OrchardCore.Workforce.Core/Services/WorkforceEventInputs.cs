using System.Globalization;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Builds the input of Workforce events.
/// </summary>
internal static class WorkforceEventInputs
{
    public static Dictionary<string, object> TimeOff(TimeOffRequest request)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["WorkerId"] = request.WorkerId,
            ["TeamId"] = request.TeamId,
            ["RequestId"] = request.ItemId,
            ["TypeId"] = request.TypeId,
            ["StartDate"] = Day(request.StartDate),
            ["EndDate"] = Day(request.EndDate),
            ["Status"] = request.Status.ToString(),
        };
    }

    public static Dictionary<string, object> Punch(TimePunch punch)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["WorkerId"] = punch.WorkerId,
            ["TeamId"] = punch.TeamId,
            ["PunchId"] = punch.ItemId,
            ["Utc"] = punch.Utc,
            ["Source"] = punch.Source.ToString(),
        };
    }

    public static Dictionary<string, object> Timesheet(Timesheet timesheet)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["WorkerId"] = timesheet.WorkerId,
            ["TeamId"] = timesheet.TeamId,
            ["TimesheetId"] = timesheet.ItemId,
            ["PeriodStart"] = Day(timesheet.PeriodStart),
            ["PeriodEnd"] = Day(timesheet.PeriodEnd),
            ["Hours"] = timesheet.TotalHours,
        };
    }

    public static Dictionary<string, object> Exception(AttendanceException exception)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["WorkerId"] = exception.WorkerId,
            ["TeamId"] = exception.TeamId,
            ["ExceptionId"] = exception.ItemId,
            ["Kind"] = exception.Kind.ToString(),
            ["Date"] = Day(exception.Date),
            ["Minutes"] = exception.Minutes,
            ["Points"] = exception.Points,
        };
    }

    private static string Day(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
