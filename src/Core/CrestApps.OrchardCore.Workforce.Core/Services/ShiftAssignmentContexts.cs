using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Builds the <see cref="ShiftAssignmentContext"/> of a worker taking a published shift or an open shift.
/// </summary>
internal sealed class ShiftAssignmentContexts
{
    private readonly ShiftTemplateStore _templateStore;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly TeamTimeZoneResolver _timeZones;

    public ShiftAssignmentContexts(ShiftTemplateStore templateStore, ActivityCodeStore activityCodeStore, TeamTimeZoneResolver timeZones)
    {
        _templateStore = templateStore;
        _activityCodeStore = activityCodeStore;
        _timeZones = timeZones;
    }

    public async Task<ShiftAssignmentContext> ForEventAsync(CalendarEvent calendarEvent, ShiftData data, WorkerProfile worker, IEnumerable<string> ignoredShiftKeys, CancellationToken cancellationToken = default)
    {
        var timeZoneId = await _timeZones.GetAsync(data.TeamId, cancellationToken);
        var codes = await CodesAsync(cancellationToken);
        var template = string.IsNullOrEmpty(data.ShiftTemplateId) ? null : await _templateStore.FindByIdAsync(data.ShiftTemplateId, cancellationToken);

        return new ShiftAssignmentContext
        {
            Worker = worker,
            TeamId = data.TeamId,
            Date = DateOnly.FromDateTime(CalendarTime.ToLocal(calendarEvent.StartUtc, timeZoneId)),
            StartUtc = calendarEvent.StartUtc,
            EndUtc = calendarEvent.EndUtc,
            PaidMinutes = ShiftEvents.PaidMinutes(calendarEvent, data, codes),
            RequiredSkillIds = template?.RequiredSkillIds.ToArray() ?? [],
            IgnoredShiftKeys = new HashSet<string>(ignoredShiftKeys.Where(key => !string.IsNullOrEmpty(key)), StringComparer.Ordinal),
        };
    }

    public async Task<ShiftAssignmentContext> ForOpenShiftAsync(OpenShift shift, WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        var codes = await CodesAsync(cancellationToken);

        return new ShiftAssignmentContext
        {
            Worker = worker,
            TeamId = shift.TeamId,
            Date = shift.Date,
            StartUtc = CalendarTime.ToUtc(shift.Start, shift.TimeZoneId),
            EndUtc = CalendarTime.ToUtc(shift.End, shift.TimeZoneId),
            PaidMinutes = ShiftEvents.PaidMinutes(shift.Start, shift.End, shift.Segments, codes),
            RequiredSkillIds = shift.RequiredSkillIds.ToArray(),
            IgnoredShiftKeys = new HashSet<string>([shift.ShiftKey ?? string.Empty], StringComparer.Ordinal),
        };
    }

    private async Task<Dictionary<string, ActivityCode>> CodesAsync(CancellationToken cancellationToken)
    {
        return (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
    }
}
