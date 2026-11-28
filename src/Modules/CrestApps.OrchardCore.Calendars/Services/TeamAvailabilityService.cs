using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Modules;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// The Team Availability page's reading of a team calendar for one day: the users in the team's role (up to a cap), the busy
/// state of each in half-hour slots, and the first times everyone is free for a meeting of a given length.
/// </summary>
public sealed class TeamAvailabilityService
{
    /// <summary>
    /// The most team members read.
    /// </summary>
    public const int MaxMembers = 50;

    /// <summary>
    /// The length of a slot of the busy grid, in minutes.
    /// </summary>
    public const int SlotMinutes = 30;

    /// <summary>
    /// The most free times found.
    /// </summary>
    public const int MaxFreeTimes = 10;

    private readonly ISession _session;
    private readonly ICalendarAvailabilityService _availabilityService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamAvailabilityService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="clock">The clock.</param>
    public TeamAvailabilityService(ISession session, ICalendarAvailabilityService availabilityService, IClock clock)
    {
        _session = session;
        _availabilityService = availabilityService;
        _clock = clock;
    }

    /// <summary>
    /// Reads a team calendar's day.
    /// </summary>
    /// <param name="teamCalendar">The team calendar.</param>
    /// <param name="day">The day in the calendar's time zone, or <see langword="null"/> for today.</param>
    /// <param name="minutes">The length of the meeting to find free times for.</param>
    /// <param name="firstHour">The first hour of the day read.</param>
    /// <param name="lastHour">The hour the day read ends.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The day's availability.</returns>
    public async Task<TeamAvailability> GetAsync(Calendar teamCalendar, DateTime? day, int minutes, int firstHour, int lastHour, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(teamCalendar);

        var timeZoneId = CalendarTime.NormalizeZoneId(teamCalendar.TimeZoneId) ?? "UTC";
        var date = day ?? CalendarTime.ToLocal(_clock.UtcNow, timeZoneId).Date;
        var fromUtc = CalendarTime.ToUtc(date.AddHours(firstHour), timeZoneId);
        var toUtc = CalendarTime.ToUtc(date.AddHours(lastHour), timeZoneId);
        var role = teamCalendar.OwnerId?.ToUpperInvariant();
        var members = string.IsNullOrEmpty(role)
            ? []
            : await _session.Query<User, UserByRoleNameIndex>(index => index.RoleName == role).Take(MaxMembers).ListAsync(cancellationToken);

        var busy = await _availabilityService.GetBusyAsync(new CalendarBusyQuery
        {
            UserIds = members.Select(member => member.UserId).ToList(),
            FromUtc = fromUtc,
            ToUtc = toUtc,
        }, cancellationToken);

        var result = new TeamAvailability
        {
            TimeZoneId = timeZoneId,
            Day = date,
            FromUtc = fromUtc,
            ToUtc = toUtc,
        };

        var slots = (lastHour - firstHour) * 60 / SlotMinutes;

        foreach (var member in members.OrderBy(member => member.UserName, StringComparer.OrdinalIgnoreCase))
        {
            var row = new TeamAvailabilityMember { UserId = member.UserId, UserName = member.UserName };
            var memberBusy = busy.Where(interval => interval.UserId == member.UserId).ToList();

            row.Busy = memberBusy;

            for (var slot = 0; slot < slots; slot++)
            {
                var slotStart = fromUtc.AddMinutes(slot * SlotMinutes);
                var slotEnd = slotStart.AddMinutes(SlotMinutes);
                var overlapping = memberBusy.Where(interval => interval.StartUtc < slotEnd && interval.EndUtc > slotStart).ToList();

                row.Slots.Add(overlapping.Count == 0 ? 0 : overlapping.All(interval => interval.Tentative) ? 1 : 2);
            }

            result.Members.Add(row);
        }

        result.FreeTimes = await _availabilityService.FindFreeWindowsAsync(new CalendarBusyQuery
        {
            UserIds = members.Select(member => member.UserId).ToList(),
            FromUtc = fromUtc,
            ToUtc = toUtc,
            IncludeTentative = false,
        }, TimeSpan.FromMinutes(minutes), maxResults: MaxFreeTimes, cancellationToken: cancellationToken);

        return result;
    }
}

/// <summary>
/// A team calendar's day.
/// </summary>
public sealed class TeamAvailability
{
    /// <summary>
    /// Gets or sets the calendar's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the day read, in the calendar's time zone.
    /// </summary>
    public DateTime Day { get; set; }

    /// <summary>
    /// Gets or sets the start of the hours read, in UTC.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the hours read, in UTC.
    /// </summary>
    public DateTime ToUtc { get; set; }

    /// <summary>
    /// Gets the team members, by user name.
    /// </summary>
    public IList<TeamAvailabilityMember> Members { get; } = [];

    /// <summary>
    /// Gets or sets the first times everyone is free, in UTC.
    /// </summary>
    public IReadOnlyList<TimeWindow> FreeTimes { get; set; } = [];
}

/// <summary>
/// A team member's busy time on the day read.
/// </summary>
public sealed class TeamAvailabilityMember
{
    /// <summary>
    /// Gets or sets the user's ID.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user's name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets the slots of the busy grid: 0 free, 1 tentatively busy, 2 busy.
    /// </summary>
    public IList<int> Slots { get; } = [];

    /// <summary>
    /// Gets or sets the busy intervals.
    /// </summary>
    public IReadOnlyList<CalendarBusyInterval> Busy { get; set; } = [];
}
