using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.Tests.Agent;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// The business hours tools go through the real Business Hours schedule manager and handler, change only the days and
/// dates given (up to two opening windows a day, and holidays and special days with their own hours and names, as the
/// schedule editor takes them), and answer open or closed through the shared business hours service.
/// </summary>
public sealed class ContactCenterBusinessHoursToolTests
{
    private readonly ContactCenterSetupToolHarness _harness = new();

    [Fact]
    public async Task SaveCalendar_ClosesTwoDatesAndChangesOneDayOnly()
    {
        // Arrange
        _harness.AddCalendar("calendar-1", "Support", calendar => calendar.Hours.DateExceptions = [new HoursDateException { Date = new DateOnly(2026, 1, 1) }]);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveBusinessHoursCalendarTool(), _harness.Build(), new()
        {
            ["id"] = "calendar-1",
            ["addHolidays"] = new[] { "2026-12-25", "2026-12-26" },
            ["weeklySchedule"] = new[] { new Dictionary<string, object> { ["day"] = "Saturday", ["open"] = "10:00", ["close"] = "14:00" } },
        }));

        // Assert
        Assert.Equal("updated", json.GetProperty("saved").GetString());
        var calendar = _harness.Calendars.Entries.Single();
        Assert.Equal([new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 26)], calendar.Hours.DateExceptions.Where(exception => exception.Closed).Select(exception => exception.Date));
        var saturday = calendar.Hours.Windows.Single(window => window.Day == DayOfWeek.Saturday);
        Assert.Equal((600, 840), (saturday.OpenMinute, saturday.CloseMinute));
        var monday = calendar.Hours.Windows.Single(window => window.Day == DayOfWeek.Monday);
        Assert.Equal((540, 1020), (monday.OpenMinute, monday.CloseMinute));
        Assert.Equal("UTC", calendar.Hours.TimeZoneId);
    }

    [Fact]
    public async Task SaveCalendar_AddsASpecialDayWithItsOwnHours_AndANamedHoliday_AndKeepsTheOtherDates()
    {
        // Arrange
        _harness.AddCalendar("calendar-1", "Support", calendar => calendar.Hours.DateExceptions = [new HoursDateException { Date = new DateOnly(2026, 1, 1), Name = "New Year's Day" }]);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveBusinessHoursCalendarTool(), _harness.Build(), new()
        {
            ["id"] = "calendar-1",
            ["addSpecialDays"] = new[]
            {
                new Dictionary<string, object> { ["date"] = "2026-12-24", ["open"] = "09:00", ["close"] = "13:00", ["name"] = "Christmas Eve" },
                new Dictionary<string, object> { ["date"] = "2026-12-25", ["closed"] = true, ["name"] = "Christmas Day" },
            },
        }));

        // Assert
        Assert.Equal("updated", json.GetProperty("saved").GetString());
        var exceptions = _harness.Calendars.Entries.Single().Hours.DateExceptions.ToDictionary(exception => exception.Date);
        Assert.Equal(3, exceptions.Count);
        Assert.True(exceptions[new DateOnly(2026, 1, 1)].Closed);
        Assert.Equal("New Year's Day", exceptions[new DateOnly(2026, 1, 1)].Name);
        var christmasEve = exceptions[new DateOnly(2026, 12, 24)];
        Assert.False(christmasEve.Closed);
        Assert.Equal((540, 780, "Christmas Eve"), (christmasEve.OpenMinute, christmasEve.CloseMinute, christmasEve.Name));
        Assert.True(exceptions[new DateOnly(2026, 12, 25)].Closed);
        Assert.Equal("Christmas Day", exceptions[new DateOnly(2026, 12, 25)].Name);

        var specialDays = json.GetProperty("calendar").GetProperty("specialDays").EnumerateArray().ToList();
        Assert.Equal(["2026-01-01", "2026-12-24", "2026-12-25"], specialDays.Select(day => day.GetProperty("date").GetString()));
        Assert.Equal("13:00", specialDays[1].GetProperty("close").GetString());
    }

    [Fact]
    public async Task SaveCalendar_SplitsADayInTwoWindows_AndTurnsAHolidayBackIntoAWorkingDay()
    {
        // Arrange
        _harness.AddCalendar("calendar-1", "Support", calendar => calendar.Hours.DateExceptions = [new HoursDateException { Date = new DateOnly(2026, 12, 26), Name = "Boxing Day" }]);

        // Act
        await AgentToolTestContext.InvokeAsync(new SaveBusinessHoursCalendarTool(), _harness.Build(), new()
        {
            ["id"] = "calendar-1",
            ["weeklySchedule"] = new[]
            {
                new Dictionary<string, object> { ["day"] = "Monday", ["open"] = "13:00", ["close"] = "17:00" },
                new Dictionary<string, object> { ["day"] = "Monday", ["open"] = "08:00", ["close"] = "12:00" },
            },
            ["removeSpecialDays"] = new[] { "2026-12-26" },
        });

        // Assert
        var calendar = _harness.Calendars.Entries.Single();
        Assert.Equal([(480, 720), (780, 1020)], calendar.Hours.Windows.Where(window => window.Day == DayOfWeek.Monday).Select(window => (window.OpenMinute, window.CloseMinute)));
        Assert.Single(calendar.Hours.Windows, window => window.Day == DayOfWeek.Tuesday);
        Assert.Empty(calendar.Hours.DateExceptions);
    }

    [Fact]
    public async Task SaveCalendar_RefusesADateGivenAsAHolidayAndASpecialDay_ASpecialDayWithoutHours_AndAThirdWindow()
    {
        // Arrange
        _harness.AddCalendar("calendar-1", "Support");

        // Act
        var both = await AgentToolTestContext.InvokeAsync(new SaveBusinessHoursCalendarTool(), _harness.Build(), new()
        {
            ["id"] = "calendar-1",
            ["addHolidays"] = new[] { "2026-12-24" },
            ["addSpecialDays"] = new[] { new Dictionary<string, object> { ["date"] = "2026-12-24", ["open"] = "09:00", ["close"] = "13:00" } },
        });
        var withoutHours = await AgentToolTestContext.InvokeAsync(new SaveBusinessHoursCalendarTool(), _harness.Build(), new()
        {
            ["id"] = "calendar-1",
            ["addSpecialDays"] = new[] { new Dictionary<string, object> { ["date"] = "2026-12-24", ["name"] = "Christmas Eve" } },
        });
        var threeWindows = await AgentToolTestContext.InvokeAsync(new SaveBusinessHoursCalendarTool(), _harness.Build(), new()
        {
            ["id"] = "calendar-1",
            ["weeklySchedule"] = new[]
            {
                new Dictionary<string, object> { ["day"] = "Friday", ["open"] = "08:00", ["close"] = "10:00" },
                new Dictionary<string, object> { ["day"] = "Friday", ["open"] = "11:00", ["close"] = "13:00" },
                new Dictionary<string, object> { ["day"] = "Friday", ["open"] = "14:00", ["close"] = "16:00" },
            },
        });

        // Assert
        Assert.Equal("Nothing was saved: 2026-12-24 is in both addHolidays and addSpecialDays; give each date once.", both);
        Assert.Equal("Nothing was saved: 2026-12-24: give open and close as HH:mm (close may be 24:00), or closed true.", withoutHours);
        Assert.Equal("Nothing was saved: Friday has 3 opening windows; a day has 2 at most.", threeWindows);
        Assert.Equal(0, _harness.Calendars.UpdateCount);
    }

    [Fact]
    public async Task SaveCalendar_RefusesAnUnknownTimeZone()
    {
        // Arrange
        _harness.AddCalendar("calendar-1", "Support");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveBusinessHoursCalendarTool(), _harness.Build(), new()
        {
            ["id"] = "calendar-1",
            ["timeZoneId"] = "Mars/Olympus",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: 'Mars/Olympus' is not a known time zone.", result);
        Assert.Equal(0, _harness.Calendars.UpdateCount);
    }

    [Fact]
    public async Task SaveCalendar_CreatesACalendarInATimeZone()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveBusinessHoursCalendarTool(), _harness.Build(), new()
        {
            ["name"] = "Sales",
            ["timeZoneId"] = "america/new_york",
            ["weeklySchedule"] = new[] { new Dictionary<string, object> { ["day"] = "Monday", ["open"] = "08:00", ["close"] = "24:00" } },
        }));

        // Assert
        Assert.Equal("created", json.GetProperty("saved").GetString());
        var calendar = Assert.Single(_harness.Calendars.Entries);
        Assert.Equal("America/New_York", calendar.Hours.TimeZoneId);
        var window = Assert.Single(calendar.Hours.Windows);
        Assert.Equal(DayOfWeek.Monday, window.Day);
        Assert.Equal((480, 1440), (window.OpenMinute, window.CloseMinute));
    }

    [Fact]
    public async Task CheckBusinessHours_SaysClosedOnAHolidayAndWhenItOpens()
    {
        // Arrange
        _harness.AddCalendar("calendar-1", "Support", calendar => calendar.Hours.DateExceptions = [new HoursDateException { Date = new DateOnly(2026, 12, 25) }]);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new CheckBusinessHoursTool(), _harness.Build(), new()
        {
            ["calendar"] = "Support",
            ["at"] = "2026-12-25T10:00",
        }));

        // Assert
        Assert.False(json.GetProperty("open").GetBoolean());
        Assert.StartsWith("2026-12-28T09:00:00", json.GetProperty("opens").GetString());
    }

    [Fact]
    public async Task CheckBusinessHours_SaysOpenNowAndWhenItCloses()
    {
        // Arrange
        _harness.AddCalendar("calendar-1", "Support");

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new CheckBusinessHoursTool(), _harness.Build(), new() { ["calendar"] = "calendar-1" }));

        // Assert
        Assert.True(json.GetProperty("open").GetBoolean());
        Assert.StartsWith("2026-11-18T17:00:00", json.GetProperty("closes").GetString());
    }

    [Fact]
    public async Task CheckBusinessHours_SaysADisabledCalendarDoesNotGateRouting()
    {
        // Arrange
        _harness.AddCalendar("calendar-1", "Support", calendar => calendar.Enabled = false);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new CheckBusinessHoursTool(), _harness.Build(), new() { ["calendar"] = "calendar-1" }));

        // Assert
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("open").ValueKind);
        Assert.Contains("disabled", json.GetProperty("explanation").GetString());
    }
}
