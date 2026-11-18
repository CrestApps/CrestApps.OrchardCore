using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;

namespace CrestApps.OrchardCore.Tests.BusinessHours;

/// <summary>
/// Open time is added and measured on the schedule: "eight business hours from Friday 4 pm" skips the evening, the
/// weekend and holidays, and a day shortened by a daylight-saving change counts the hours it really has.
/// </summary>
public sealed class OpenTimeArithmeticTests
{
    // Friday 6 November 2026.
    private static readonly DateOnly _friday = new(2026, 11, 6);

    [Fact]
    public void AddingOpenTime_SkipsTheEveningAndTheWeekend()
    {
        var schedule = Weekdays(9, 17);

        var due = HoursScheduleEvaluator.AddOpenTime(schedule, Utc(_friday, 16, 0), TimeSpan.FromHours(8));

        Assert.Equal(Utc(_friday.AddDays(3), 16, 0), due);
    }

    [Fact]
    public void AddingOpenTime_FromAClosedMoment_StartsAtTheNextOpening()
    {
        var schedule = Weekdays(9, 17);
        var saturday = _friday.AddDays(1);

        var due = HoursScheduleEvaluator.AddOpenTime(schedule, Utc(saturday, 13, 30), TimeSpan.FromHours(2));

        Assert.Equal(Utc(_friday.AddDays(3), 11, 0), due);
    }

    [Fact]
    public void AddingOpenTime_SkipsAHoliday()
    {
        var schedule = Weekdays(9, 17);
        schedule.DateExceptions.Add(new HoursDateException { Date = _friday.AddDays(3), Name = "Holiday", Closed = true });

        var due = HoursScheduleEvaluator.AddOpenTime(schedule, Utc(_friday, 16, 0), TimeSpan.FromHours(2));

        Assert.Equal(Utc(_friday.AddDays(4), 10, 0), due);
    }

    [Fact]
    public void AddingNoTime_ReturnsTheStart_AndAScheduleThatNeverOpensHasNoAnswer()
    {
        var start = Utc(_friday, 20, 0);

        Assert.Equal(start, HoursScheduleEvaluator.AddOpenTime(Weekdays(9, 17), start, TimeSpan.Zero));
        Assert.Null(HoursScheduleEvaluator.AddOpenTime(new HoursSchedule(), start, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void OpenTimeBetweenTwoInstants_CountsOnlyOpenHours()
    {
        var schedule = Weekdays(9, 17);

        var open = HoursScheduleEvaluator.GetOpenTime(schedule, Utc(_friday, 16, 0), Utc(_friday.AddDays(3), 10, 0));

        Assert.Equal(TimeSpan.FromHours(2), open);
        Assert.Equal(TimeSpan.Zero, HoursScheduleEvaluator.GetOpenTime(schedule, Utc(_friday, 16, 0), Utc(_friday, 15, 0)));
    }

    [Fact]
    public void ADayShortenedByDaylightSaving_CountsTheHoursItHas()
    {
        // Sunday 8 March 2026 in New York: 2 am to 3 am doesn't exist, so midnight to 6 am is five hours long.
        var sunday = new DateOnly(2026, 3, 8);
        var schedule = new HoursSchedule
        {
            TimeZoneId = "America/New_York",
            Windows = [new HoursWindow { Day = DayOfWeek.Sunday, OpenMinute = 0, CloseMinute = 6 * 60 }],
        };
        var midnightLocal = Utc(sunday, 5, 0);

        var due = HoursScheduleEvaluator.AddOpenTime(schedule, midnightLocal, TimeSpan.FromHours(5));
        var open = HoursScheduleEvaluator.GetOpenTime(schedule, midnightLocal, Utc(sunday, 12, 0));

        Assert.Equal(Utc(sunday, 10, 0), due);
        Assert.Equal(TimeSpan.FromHours(5), open);
    }

    [Fact]
    public async Task TheService_CountsAroundTheClock_WhenNobodyKnowsTheSchedule()
    {
        // Arrange
        var service = new DefaultOpenHoursService([new Source("support", Weekdays(9, 17))]);
        var start = Utc(_friday, 16, 0);

        // Act
        var onSchedule = await service.AddOpenTimeAsync("support", start, TimeSpan.FromHours(2), cancellationToken: TestContext.Current.CancellationToken);
        var unknown = await service.AddOpenTimeAsync("missing", start, TimeSpan.FromHours(2), cancellationToken: TestContext.Current.CancellationToken);
        var noSchedule = await service.AddOpenTimeAsync(null, start, TimeSpan.FromHours(2), cancellationToken: TestContext.Current.CancellationToken);
        var between = await service.GetOpenTimeBetweenAsync("support", start, Utc(_friday.AddDays(3), 10, 0), cancellationToken: TestContext.Current.CancellationToken);
        var betweenUnknown = await service.GetOpenTimeBetweenAsync("missing", start, start.AddHours(3), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Utc(_friday.AddDays(3), 10, 0), onSchedule);
        Assert.Equal(start.AddHours(2), unknown);
        Assert.Equal(start.AddHours(2), noSchedule);
        Assert.Equal(TimeSpan.FromHours(2), between);
        Assert.Equal(TimeSpan.FromHours(3), betweenUnknown);
    }

    private static HoursSchedule Weekdays(int openHour, int closeHour)
        => new()
        {
            Windows =
            [
                .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                    .Select(day => new HoursWindow { Day = day, OpenMinute = openHour * 60, CloseMinute = closeHour * 60 }),
            ],
        };

    private static DateTime Utc(DateOnly date, int hour, int minute)
        => new(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Utc);

    private sealed class Source : IHoursScheduleSource
    {
        private readonly string _id;
        private readonly HoursSchedule _schedule;

        public Source(string id, HoursSchedule schedule)
        {
            _id = id;
            _schedule = schedule;
        }

        public Task<HoursSchedule> FindAsync(string scheduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(scheduleId == _id ? _schedule : null);

        public Task<IReadOnlyList<HoursScheduleOption>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HoursScheduleOption>>([new HoursScheduleOption(_id, _id)]);
    }
}
