using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;

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

    [Fact]
    public void R6L26_AnAnonymousResult_IsItsCompletionOnly()
    {
        // Arrange: a quiz result with a score and a pass.
        var result = new CourseItemResult { ItemId = "quiz1", Score = 73.5m, Passed = true, PassDecided = true, Submitted = true, Graded = true, Attempts = 2 };

        // Act
        var anonymous = LearningSurveyResultObserver.CompletionOnly(result);

        // Assert: no score or pass reaches the gradebook; the work counts as done in every completion mode that asks for it.
        Assert.Equal("quiz1", anonymous.ItemId);
        Assert.Null(anonymous.Score);
        Assert.Null(anonymous.Passed);
        Assert.True(anonymous.PassDecided);
        Assert.True(anonymous.CompletedWithoutPassDecision);
        Assert.True(anonymous.Submitted);
        Assert.True(anonymous.Graded);
        Assert.False(anonymous.ResetScore);
        Assert.Equal(2, anonymous.Attempts);

        // R7-M8: the item stays out of the course grade instead of counting as zero.
        Assert.True(anonymous.CompletionOnly);
    }

    [Fact]
    public void R6L26_AnAnonymousAttemptWaitingForAGrader_StillCompletes()
    {
        // Act: an attempt handed in whose essay waits for a grader; a later grade of an anonymous response reaches nobody.
        var anonymous = LearningSurveyResultObserver.CompletionOnly(new CourseItemResult { ItemId = "quiz1", Submitted = true, ResetScore = true, PassDecided = true });

        // Assert
        Assert.True(anonymous.Submitted);
        Assert.True(anonymous.Graded);
        Assert.True(anonymous.CompletedWithoutPassDecision);
        Assert.False(anonymous.ResetScore);
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
