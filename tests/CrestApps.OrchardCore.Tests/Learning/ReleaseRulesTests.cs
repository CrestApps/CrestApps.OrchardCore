using System.Globalization;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class ReleaseRulesTests
{
    private static readonly DateTime _now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, CourseItemState.InProgress, null, false)]
    [InlineData(null, CourseItemState.Completed, null, true)]
    [InlineData("80", CourseItemState.Completed, "79.5", false)]
    [InlineData("80", CourseItemState.Completed, "80", true)]
    [InlineData("80", CourseItemState.Completed, null, false)]
    public async Task ItemCompleted_IsMetWhenTheOtherItemIsCompletedWithTheScore(string minimumScore, CourseItemState state, string score, bool released)
    {
        // Arrange
        var rule = new ItemCompletedReleaseRule();
        var settings = new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted, Values = { ["ItemId"] = "first" } };

        if (minimumScore is not null)
        {
            settings.Values["MinimumScore"] = minimumScore;
        }

        var progress = new EnrollmentProgress
        {
            Items = [new CourseItemProgress { ContentItemId = "first", State = state, Score = score is null ? null : decimal.Parse(score, CultureInfo.InvariantCulture) }],
        };

        // Act
        var decision = await rule.EvaluateAsync(new ReleaseRuleContext { Settings = settings, Progress = progress, Enrollment = new Enrollment(), UtcNow = _now });

        // Assert
        Assert.Equal(released, decision.IsReleased);
    }

    [Fact]
    public async Task Date_IsMetFromTheDate_AndTellsWhenItOpens()
    {
        // Arrange
        var rule = new DateReleaseRule();
        var opens = _now.AddDays(2);
        var settings = new LearningRuleSettings { Name = LearningRuleNames.Date, Values = { ["FromUtc"] = opens.ToString("O", CultureInfo.InvariantCulture) } };

        // Act
        var before = await rule.EvaluateAsync(new ReleaseRuleContext { Settings = settings, UtcNow = _now, Enrollment = new Enrollment() });
        var after = await rule.EvaluateAsync(new ReleaseRuleContext { Settings = settings, UtcNow = opens, Enrollment = new Enrollment() });

        // Assert
        Assert.False(before.IsReleased);
        Assert.Equal(opens, before.AvailableFromUtc);
        Assert.True(after.IsReleased);
    }

    [Fact]
    public async Task DaysAfterEnrollment_CountsFromTheEnrollment()
    {
        // Arrange
        var rule = new DaysAfterEnrollmentReleaseRule();
        var settings = new LearningRuleSettings { Name = LearningRuleNames.DaysAfterEnrollment, Values = { ["Days"] = "7" } };
        var enrollment = new Enrollment { CreatedUtc = _now };

        // Act
        var early = await rule.EvaluateAsync(new ReleaseRuleContext { Settings = settings, Enrollment = enrollment, UtcNow = _now.AddDays(6) });
        var due = await rule.EvaluateAsync(new ReleaseRuleContext { Settings = settings, Enrollment = enrollment, UtcNow = _now.AddDays(7) });

        // Assert
        Assert.False(early.IsReleased);
        Assert.Equal(_now.AddDays(7), early.AvailableFromUtc);
        Assert.True(due.IsReleased);
    }

    [Fact]
    public async Task RulesWithBrokenSettings_StayLocked()
    {
        // Arrange
        var context = new ReleaseRuleContext { Settings = new LearningRuleSettings(), Enrollment = new Enrollment { CreatedUtc = _now }, Progress = new EnrollmentProgress(), UtcNow = _now };

        // Act & Assert
        Assert.False((await new ItemCompletedReleaseRule().EvaluateAsync(context)).IsReleased);
        Assert.False((await new DateReleaseRule().EvaluateAsync(context)).IsReleased);
        Assert.False((await new DaysAfterEnrollmentReleaseRule().EvaluateAsync(context)).IsReleased);
    }
}
