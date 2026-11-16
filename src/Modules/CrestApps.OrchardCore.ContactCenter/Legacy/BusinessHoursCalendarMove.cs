using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Legacy;

/// <summary>
/// Moves the Contact Center's business hours calendars to the Business Hours module: each calendar becomes a
/// <see cref="BusinessHoursSchedule"/> with the same id, so every queue, entry point, dialing profile and activity batch
/// that names the calendar keeps its hours. A calendar's open days become weekly windows and its holidays dates closed
/// all day, which the shared open-hours service reads exactly as it read the calendar.
/// </summary>
internal sealed class BusinessHoursCalendarMove
{
    /// <summary>
    /// The type name YesSql stored on each calendar document, whose class no longer exists.
    /// </summary>
    internal const string StoredTypeName = "CrestApps.OrchardCore.ContactCenter.Core.Models.BusinessHoursCalendar, CrestApps.OrchardCore.ContactCenter.Core";

    private readonly ISession _session;
    private readonly INamedCatalogManager<BusinessHoursSchedule> _schedules;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarMove"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="schedules">The Business Hours schedule manager.</param>
    public BusinessHoursCalendarMove(ISession session, INamedCatalogManager<BusinessHoursSchedule> schedules)
    {
        _session = session;
        _schedules = schedules;
    }

    /// <summary>
    /// Copies every stored calendar to a schedule with the same id. A calendar whose id a schedule already has is left
    /// alone, so running it again does nothing more. A calendar whose name another schedule has gets a number added.
    /// The calendar documents stay where they are.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many calendars were copied.</returns>
    public async Task<int> MoveAsync(CancellationToken cancellationToken = default)
    {
        // A schedule created in this scope can't be read back until it commits, so the names and ids used are kept here.
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var moved = 0;

        foreach (var calendar in await ListStoredCalendarsAsync(cancellationToken))
        {
            if (string.IsNullOrEmpty(calendar.ItemId)
                || !ids.Add(calendar.ItemId)
                || await _schedules.FindByIdAsync(calendar.ItemId, cancellationToken) is not null)
            {
                continue;
            }

            var schedule = await _schedules.NewAsync(cancellationToken: cancellationToken);

            schedule.ItemId = calendar.ItemId;
            Apply(calendar, schedule);
            schedule.Name = await UniqueNameAsync(string.IsNullOrWhiteSpace(calendar.Name) ? calendar.ItemId : calendar.Name.Trim(), names, cancellationToken);
            schedule.ModifiedUtc = calendar.ModifiedUtc;

            if (calendar.CreatedUtc != default)
            {
                schedule.CreatedUtc = calendar.CreatedUtc;
            }

            await _schedules.CreateAsync(schedule, cancellationToken);
            moved++;
        }

        return moved;
    }

    /// <summary>
    /// Creates or updates the schedule of a calendar an older recipe carries, with the same checks as the Business Hours
    /// editor. The schedule is found by the calendar's id, then by its name; a new one keeps the calendar's id when it is
    /// a valid id. A refused calendar is not saved.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The schedule, or the reasons the calendar was refused.</returns>
    public async Task<(BusinessHoursSchedule Schedule, IReadOnlyList<string> Errors)> ImportAsync(LegacyBusinessHoursCalendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var schedule = string.IsNullOrEmpty(calendar.ItemId) ? null : await _schedules.FindByIdAsync(calendar.ItemId, cancellationToken);

        if (schedule is null && !string.IsNullOrWhiteSpace(calendar.Name))
        {
            schedule = await _schedules.FindByNameAsync(calendar.Name.Trim(), cancellationToken);
        }

        var isNew = schedule is null;

        if (isNew)
        {
            schedule = await _schedules.NewAsync(cancellationToken: cancellationToken);

            if (!string.IsNullOrEmpty(calendar.ItemId) && UniqueId.IsValid(calendar.ItemId))
            {
                schedule.ItemId = calendar.ItemId;
            }
        }

        Apply(calendar, schedule);
        schedule.Name = calendar.Name?.Trim();

        var validation = await _schedules.ValidateAsync(schedule, cancellationToken);

        if (!validation.Succeeded)
        {
            return (null, [.. validation.Errors.Select(error => error.ErrorMessage)]);
        }

        if (isNew)
        {
            await _schedules.CreateAsync(schedule, cancellationToken);
        }
        else
        {
            await _schedules.UpdateAsync(schedule, cancellationToken: cancellationToken);
        }

        return (schedule, []);
    }

    /// <summary>
    /// Gives the hours of a calendar: its open days become weekly windows and its holidays dates closed all day.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <returns>The hours.</returns>
    internal static HoursSchedule ToHoursSchedule(LegacyBusinessHoursCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return new HoursSchedule
        {
            TimeZoneId = string.IsNullOrWhiteSpace(calendar.TimeZoneId) ? null : calendar.TimeZoneId.Trim(),
            Windows = [.. (calendar.WeeklySchedule ?? [])
                .Where(day => day is not null && day.IsOpen)
                .Select(day => new HoursWindow { Day = day.Day, OpenMinute = day.OpenMinute, CloseMinute = day.CloseMinute })],
            DateExceptions = [.. (calendar.Holidays ?? [])
                .Distinct()
                .Order()
                .Select(date => new HoursDateException { Date = date, Closed = true })],
        };
    }

    private static void Apply(LegacyBusinessHoursCalendar calendar, BusinessHoursSchedule schedule)
    {
        schedule.Description = string.IsNullOrWhiteSpace(calendar.Description) ? null : calendar.Description.Trim();
        schedule.Enabled = calendar.Enabled;
        schedule.Hours = ToHoursSchedule(calendar);
    }

    // The calendar's name, with a number added when another schedule, or one made earlier in this run, has it.
    private async Task<string> UniqueNameAsync(string baseName, HashSet<string> names, CancellationToken cancellationToken)
    {
        var name = baseName;

        for (var number = 2; names.Contains(name) || await _schedules.FindByNameAsync(name, cancellationToken) is not null; number++)
        {
            name = $"{baseName} ({number})";
        }

        names.Add(name);

        return name;
    }

    private async Task<IReadOnlyList<LegacyBusinessHoursCalendar>> ListStoredCalendarsAsync(CancellationToken cancellationToken)
    {
        // The calendars are stored under the old class name, which no longer exists: read that name as the legacy model.
        // Only the reading is mapped; the stored rows are left as they are.
        _session.Store.TypeNames[typeof(LegacyBusinessHoursCalendar)] = StoredTypeName;

        var calendars = await _session.Query<LegacyBusinessHoursCalendar>(ContactCenterStorage.CollectionName).ListAsync(cancellationToken);

        return [.. calendars.Where(calendar => calendar is not null)];
    }
}
