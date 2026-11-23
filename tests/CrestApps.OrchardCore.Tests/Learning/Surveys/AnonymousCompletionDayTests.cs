using CrestApps.OrchardCore.Learning.Core.Surveys;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// Production review P4-M5: an anonymous course survey's completion reaches the learner's progress as the start of the
/// day in the site's time zone, never the minute the anonymous response came in.
/// </summary>
public sealed class AnonymousCompletionDayTests
{
    [Fact]
    public void P4M5_TheDayStartsAtMidnight_InTheSitesTimeZone()
    {
        // Arrange: 03:30 UTC on 10 January 2026 is still 9 January in New York (UTC-5).
        var utc = new DateTime(2026, 1, 10, 3, 30, 0, DateTimeKind.Utc);

        // Act
        var day = LearningSurveyResultObserver.StartOfDayUtc(utc, "America/New_York");

        // Assert
        Assert.Equal(new DateTime(2026, 1, 9, 5, 0, 0, DateTimeKind.Utc), day);
        Assert.Equal(DateTimeKind.Utc, day.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public void P4M5_AnUnknownZone_UsesUtcDays(string zone)
    {
        // Act
        var day = LearningSurveyResultObserver.StartOfDayUtc(new DateTime(2026, 5, 4, 22, 15, 7, DateTimeKind.Utc), zone);

        // Assert
        Assert.Equal(new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc), day);
    }
}
