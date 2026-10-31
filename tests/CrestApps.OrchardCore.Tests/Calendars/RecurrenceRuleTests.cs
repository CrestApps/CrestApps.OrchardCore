using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using NodaTime;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// Parsing, writing and expanding RFC 5545 recurrence rules.
/// </summary>
public sealed class RecurrenceRuleTests
{
    private static readonly DateTimeZone _newYork = DateTimeZoneProviders.Tzdb["America/New_York"];

    [Theory]
    [InlineData("FREQ=DAILY")]
    [InlineData("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE,FR")]
    [InlineData("FREQ=MONTHLY;COUNT=10;BYDAY=-1FR")]
    [InlineData("FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=-1")]
    [InlineData("FREQ=YEARLY;UNTIL=20301231T235959Z;BYMONTH=11;BYDAY=4TH")]
    [InlineData("FREQ=WEEKLY;UNTIL=20261231;BYDAY=SU;WKST=SU")]
    public void Parse_ThenToString_RoundTrips(string rule)
    {
        // Act
        var parsed = RecurrenceRule.Parse(rule);

        // Assert
        Assert.Equal(rule, parsed.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("INTERVAL=2")]
    [InlineData("FREQ=HOURLY")]
    [InlineData("FREQ=DAILY;COUNT=0")]
    [InlineData("FREQ=DAILY;COUNT=3;UNTIL=20261231")]
    [InlineData("FREQ=WEEKLY;BYDAY=2MO")]
    [InlineData("FREQ=DAILY;BYHOUR=9")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=32")]
    public void TryParse_RefusesInvalidOrUnsupportedRules(string rule)
    {
        // Act
        var parsed = RecurrenceRule.TryParse(rule, out _, out var error);

        // Assert
        Assert.False(parsed);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Daily_WithCount_StopsAfterCount()
    {
        // Arrange
        var rule = RecurrenceRule.Parse("RRULE:FREQ=DAILY;INTERVAL=2;COUNT=3");

        // Act
        var dates = Expand(rule, new LocalDateTime(2026, 3, 1, 9, 0));

        // Assert
        Assert.Equal(["2026-03-01T09:00", "2026-03-03T09:00", "2026-03-05T09:00"], dates);
    }

    [Fact]
    public void Weekly_ByDay_ListsEachDayInOrder()
    {
        // Arrange (2026-11-02 is a Monday)
        var rule = RecurrenceRule.Parse("FREQ=WEEKLY;BYDAY=MO,WE;COUNT=5");

        // Act
        var dates = Expand(rule, new LocalDateTime(2026, 11, 2, 10, 30));

        // Assert
        Assert.Equal(["2026-11-02T10:30", "2026-11-04T10:30", "2026-11-09T10:30", "2026-11-11T10:30", "2026-11-16T10:30"], dates);
    }

    [Fact]
    public void Weekly_EveryOtherWeek_SkipsWeeks()
    {
        // Arrange
        var rule = RecurrenceRule.Parse("FREQ=WEEKLY;INTERVAL=2;BYDAY=TU;COUNT=3");

        // Act
        var dates = Expand(rule, new LocalDateTime(2026, 11, 3, 8, 0));

        // Assert
        Assert.Equal(["2026-11-03T08:00", "2026-11-17T08:00", "2026-12-01T08:00"], dates);
    }

    [Fact]
    public void Monthly_LastFriday_UsesNegativeOrdinal()
    {
        // Arrange
        var rule = RecurrenceRule.Parse("FREQ=MONTHLY;BYDAY=-1FR;COUNT=3");

        // Act
        var dates = Expand(rule, new LocalDateTime(2026, 10, 30, 16, 0));

        // Assert
        Assert.Equal(["2026-10-30T16:00", "2026-11-27T16:00", "2026-12-25T16:00"], dates);
    }

    [Fact]
    public void Monthly_OnThe31st_SkipsShortMonths()
    {
        // Arrange
        var rule = RecurrenceRule.Parse("FREQ=MONTHLY;COUNT=4");

        // Act
        var dates = Expand(rule, new LocalDateTime(2027, 1, 31, 12, 0));

        // Assert
        Assert.Equal(["2027-01-31T12:00", "2027-03-31T12:00", "2027-05-31T12:00", "2027-07-31T12:00"], dates);
    }

    [Fact]
    public void Monthly_LastWorkday_UsesSetPosition()
    {
        // Arrange
        var rule = RecurrenceRule.Parse("FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=-1;COUNT=3");

        // Act
        var dates = Expand(rule, new LocalDateTime(2026, 10, 30, 17, 0));

        // Assert (October 31 2026 is a Saturday, January 31 2027 a Sunday)
        Assert.Equal(["2026-10-30T17:00", "2026-11-30T17:00", "2026-12-31T17:00"], dates);
    }

    [Fact]
    public void Yearly_FourthThursdayOfNovember_FindsThanksgiving()
    {
        // Arrange
        var rule = RecurrenceRule.Parse("FREQ=YEARLY;BYMONTH=11;BYDAY=4TH;COUNT=3");

        // Act
        var dates = Expand(rule, new LocalDateTime(2026, 11, 26, 0, 0));

        // Assert
        Assert.Equal(["2026-11-26T00:00", "2027-11-25T00:00", "2028-11-23T00:00"], dates);
    }

    [Fact]
    public void Yearly_OnFebruary29_OnlyRunsInLeapYears()
    {
        // Arrange
        var rule = RecurrenceRule.Parse("FREQ=YEARLY;COUNT=3");

        // Act
        var dates = Expand(rule, new LocalDateTime(2028, 2, 29, 9, 0));

        // Assert
        Assert.Equal(["2028-02-29T09:00", "2032-02-29T09:00", "2036-02-29T09:00"], dates);
    }

    [Fact]
    public void Until_InUtc_IsInclusive()
    {
        // Arrange (9:00 in New York in November is 14:00 UTC)
        var rule = RecurrenceRule.Parse("FREQ=DAILY;UNTIL=20261104T140000Z");

        // Act
        var dates = Expand(rule, new LocalDateTime(2026, 11, 2, 9, 0));

        // Assert
        Assert.Equal(["2026-11-02T09:00", "2026-11-03T09:00", "2026-11-04T09:00"], dates);
    }

    [Fact]
    public void Until_AsDate_IncludesTheWholeLastDay()
    {
        // Arrange
        var rule = RecurrenceRule.Parse("FREQ=DAILY;UNTIL=20261103");

        // Act
        var dates = Expand(rule, new LocalDateTime(2026, 11, 1, 23, 0));

        // Assert
        Assert.Equal(["2026-11-01T23:00", "2026-11-02T23:00", "2026-11-03T23:00"], dates);
    }

    [Fact]
    public void RuleThatNeverMatches_EndsAfterTheFirstOccurrence()
    {
        // Arrange
        var rule = RecurrenceRule.Parse("FREQ=YEARLY;BYMONTH=2;BYMONTHDAY=30");

        // Act
        var dates = Expand(rule, new LocalDateTime(2026, 1, 5, 9, 0), take: 5);

        // Assert
        Assert.Equal(["2026-01-05T09:00"], dates);
    }

    private static List<string> Expand(RecurrenceRule rule, LocalDateTime start, int take = 100)
    {
        return RecurrenceExpander.GetOccurrences(rule, start, _newYork)
            .Take(take)
            .Select(date => date.ToString("yyyy-MM-dd'T'HH:mm", null))
            .ToList();
    }
}
