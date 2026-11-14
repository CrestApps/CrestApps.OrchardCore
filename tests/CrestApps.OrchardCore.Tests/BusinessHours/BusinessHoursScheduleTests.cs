using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Core.Handlers;
using CrestApps.OrchardCore.BusinessHours.Core.Models;
using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.BusinessHours;

/// <summary>
/// The schedules the Business Hours module keeps: the editor's weekly rows and date-exception text read back exactly
/// what they show, a disabled schedule is unknown to the open-hours service, and the rules refuse a duplicate name and
/// an unknown time zone, and a new schedule keeps the site's time zone.
/// </summary>
public sealed class BusinessHoursScheduleTests
{
    [Fact]
    public void WeeklyRows_ForANewSchedule_OfferWeekdayOfficeHours()
    {
        var rows = WeeklyHoursRows.FromWindows([]);

        Assert.Equal(7, rows.Count);
        Assert.Equal(DayOfWeek.Monday, rows[0].Day);
        Assert.Equal(DayOfWeek.Sunday, rows[6].Day);
        Assert.All(rows.Take(5), row => Assert.True(row.Open));
        Assert.False(rows[5].Open);
        Assert.False(rows[6].Open);
        Assert.Equal("09:00", rows[0].From);
        Assert.Equal("17:00", rows[0].To);
    }

    [Fact]
    public void WeeklyRows_KeepALunchBreak_ThroughTheEditor()
    {
        HoursWindow[] windows =
        [
            new() { Day = DayOfWeek.Tuesday, OpenMinute = 13 * 60, CloseMinute = 17 * 60 },
            new() { Day = DayOfWeek.Tuesday, OpenMinute = 8 * 60, CloseMinute = 12 * 60 },
        ];

        var rows = WeeklyHoursRows.FromWindows(windows);
        var tuesday = rows.Single(row => row.Day == DayOfWeek.Tuesday);
        var back = WeeklyHoursRows.ToWindows(rows);

        Assert.True(tuesday.Open);
        Assert.Equal("08:00", tuesday.From);
        Assert.Equal("12:00", tuesday.To);
        Assert.Equal("13:00", tuesday.SecondFrom);
        Assert.Equal("17:00", tuesday.SecondTo);
        Assert.False(rows.Single(row => row.Day == DayOfWeek.Monday).Open);
        Assert.Equal(2, back.Count);
        Assert.Contains(back, window => window.OpenMinute == 480 && window.CloseMinute == 720);
        Assert.Contains(back, window => window.OpenMinute == 780 && window.CloseMinute == 1020);
    }

    [Fact]
    public void WeeklyRows_WithAnUnreadableTime_NameTheDay()
    {
        var invalid = new List<DayOfWeek>();
        WeeklyHoursRow[] rows =
        [
            new() { Day = DayOfWeek.Monday, Open = true, From = "9am?", To = "17:00" },
            new() { Day = DayOfWeek.Friday, Open = true, From = "09:00", To = "17:00", SecondFrom = "18:00" },
            new() { Day = DayOfWeek.Sunday, Open = false, From = "nonsense" },
        ];

        var windows = WeeklyHoursRows.ToWindows(rows, invalid);

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], invalid);
        Assert.Single(windows);
    }

    [Fact]
    public void DateExceptionText_ReadsClosedDaysSpecialHoursAndNames()
    {
        var invalid = new List<int>();

        var exceptions = HoursDateExceptionText.Parse(
            """
            2026-12-25 Christmas Day

            2026-12-24 09:00-13:00 Christmas Eve
            2026-01-01
            not a date
            2026-07-03 25:00-26:00
            """,
            invalid);

        Assert.Equal([5, 6], invalid);
        Assert.Equal(3, exceptions.Count);

        var newYear = exceptions[0];
        Assert.Equal(new DateOnly(2026, 1, 1), newYear.Date);
        Assert.True(newYear.Closed);
        Assert.Null(newYear.Name);

        var eve = exceptions[1];
        Assert.False(eve.Closed);
        Assert.Equal(540, eve.OpenMinute);
        Assert.Equal(780, eve.CloseMinute);
        Assert.Equal("Christmas Eve", eve.Name);

        Assert.Equal("Christmas Day", exceptions[2].Name);
        Assert.True(exceptions[2].Closed);
    }

    [Fact]
    public void DateExceptionText_WritesWhatItReads()
    {
        HoursDateException[] exceptions =
        [
            new() { Date = new DateOnly(2026, 12, 25), Name = "Christmas Day" },
            new() { Date = new DateOnly(2026, 12, 24), Closed = false, OpenMinute = 540, CloseMinute = 780, Name = "Christmas Eve" },
        ];

        var text = HoursDateExceptionText.Format(exceptions);
        var back = HoursDateExceptionText.Parse(text);

        Assert.Equal($"2026-12-24 09:00-13:00 Christmas Eve{Environment.NewLine}2026-12-25 Christmas Day", text);
        Assert.Equal(2, back.Count);
        Assert.Null(HoursDateExceptionText.Format([]));
    }

    [Fact]
    public async Task Source_DoesNotKnowADisabledSchedule()
    {
        var store = new InMemoryNamedCatalog<BusinessHoursSchedule>();
        store.Entries.Add(new BusinessHoursSchedule { ItemId = "on", Name = "Support", Hours = new HoursSchedule { TimeZoneId = "Europe/Paris" } });
        store.Entries.Add(new BusinessHoursSchedule { ItemId = "off", Name = "Archive", Enabled = false });
        store.Entries.Add(new BusinessHoursSchedule { ItemId = "a", Name = "Accounts" });
        var source = new BusinessHoursScheduleSource(store);

        var found = await source.FindAsync("on", TestContext.Current.CancellationToken);
        var disabled = await source.FindAsync("off", TestContext.Current.CancellationToken);
        var listed = await source.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Europe/Paris", found.TimeZoneId);
        Assert.Null(disabled);
        Assert.Equal(["a", "on"], listed.Select(option => option.Id));
    }

    [Fact]
    public async Task Rules_RefuseADuplicateNameAndAnUnknownTimeZone()
    {
        var store = new InMemoryNamedCatalog<BusinessHoursSchedule>();
        store.Entries.Add(new BusinessHoursSchedule { ItemId = "existing", Name = "Support" });
        var handler = CreateHandler(store);
        var schedule = new BusinessHoursSchedule
        {
            ItemId = "new",
            Name = " support ",
            Hours = new HoursSchedule { TimeZoneId = "Mars/Olympus" },
        };
        var context = new ValidatingContext<BusinessHoursSchedule>(schedule);

        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(BusinessHoursSchedule.Name)));
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(HoursSchedule.TimeZoneId)));
        Assert.Equal("support", schedule.Name);
    }

    [Fact]
    public async Task Rules_AcceptAValidSchedule_AndOrderItsWindowsFromMonday()
    {
        var handler = CreateHandler(new InMemoryNamedCatalog<BusinessHoursSchedule>());
        var schedule = new BusinessHoursSchedule
        {
            ItemId = "new",
            Name = "Support",
            Hours = new HoursSchedule
            {
                TimeZoneId = "America/New_York",
                Windows =
                [
                    new HoursWindow { Day = DayOfWeek.Sunday, OpenMinute = 600, CloseMinute = 900 },
                    new HoursWindow { Day = DayOfWeek.Monday, OpenMinute = 540, CloseMinute = 1020 },
                ],
            },
        };
        var context = new ValidatingContext<BusinessHoursSchedule>(schedule);

        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.Result.Succeeded);
        Assert.Equal(DayOfWeek.Monday, schedule.Hours.Windows[0].Day);
    }

    [Fact]
    public async Task ANewSchedule_KeepsTheSitesTimeZone_NotUtc()
    {
        // Arrange
        var handler = CreateHandler(new InMemoryNamedCatalog<BusinessHoursSchedule>(), siteTimeZone: "America/Chicago");
        var schedule = new BusinessHoursSchedule();

        // Act
        await handler.InitializedAsync(new InitializedContext<BusinessHoursSchedule>(schedule), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("America/Chicago", schedule.Hours.TimeZoneId);
    }

    private static BusinessHoursScheduleHandler CreateHandler(INamedCatalog<BusinessHoursSchedule> store, string siteTimeZone = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc));

        var site = new Mock<ISite>();
        site.SetupGet(value => value.TimeZoneId).Returns(siteTimeZone);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new BusinessHoursScheduleHandler(store, clock.Object, siteService.Object, [], new PassThroughLocalizer<BusinessHoursScheduleHandler>());
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
