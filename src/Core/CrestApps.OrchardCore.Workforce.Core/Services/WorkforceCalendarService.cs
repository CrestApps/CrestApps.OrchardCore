using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Keeps each team's Workforce calendar: one per team, found by its source, named after the team.
/// </summary>
internal sealed class WorkforceCalendarService
{
    private readonly ICalendarService _calendarService;

    public WorkforceCalendarService(ICalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    public async Task EnsureCalendarAsync(WorkforceTeam team, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        if (string.IsNullOrEmpty(team.ItemId))
        {
            team.ItemId = CrestApps.Core.UniqueId.GenerateId();
        }

        var calendar = await _calendarService.FindBySourceAsync(WorkforceConstants.Source, team.ItemId, cancellationToken);

        if (calendar is null)
        {
            calendar = await _calendarService.NewAsync(WorkforceConstants.CalendarType, cancellationToken);

            if (calendar is null)
            {
                return;
            }

            Apply(team, calendar);
            calendar.Source = WorkforceConstants.Source;
            calendar.SourceId = team.ItemId;

            var created = await _calendarService.CreateAsync(calendar, new CalendarChangeOptions { Actor = WorkforceConstants.Source }, cancellationToken);

            if (created.Succeeded)
            {
                team.CalendarId = created.Value.ItemId;
            }

            return;
        }

        team.CalendarId = calendar.ItemId;

        if (calendar.Name != team.Name || calendar.TimeZoneId != (team.TimeZoneId ?? calendar.TimeZoneId) || calendar.Enabled != team.Enabled)
        {
            Apply(team, calendar);
            await _calendarService.UpdateAsync(calendar, new CalendarChangeOptions { Actor = WorkforceConstants.Source }, cancellationToken);
        }
    }

    private static void Apply(WorkforceTeam team, Calendar calendar)
    {
        calendar.Name = team.Name;
        calendar.Enabled = team.Enabled;

        if (!string.IsNullOrEmpty(team.TimeZoneId))
        {
            calendar.TimeZoneId = team.TimeZoneId;
        }
    }
}
