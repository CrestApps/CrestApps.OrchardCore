using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Upgrades;

/// <summary>
/// A quiz from the first Learning release kept its settings on the quiz part. The upgrade gives it a survey with those
/// settings and points every version of the quiz at it.
/// </summary>
public sealed class LegacyQuizUpgradeTests
{
    [Fact]
    public void ApplySettings_CarriesThePassMarkTimeLimitAttemptsAndWindow()
    {
        // Arrange
        var survey = new Survey();
        var legacy = new JsonObject
        {
            ["PassMark"] = 70m,
            ["TimeLimitMinutes"] = 30,
            ["MaxAttempts"] = 3,
            ["OpensUtc"] = "2026-12-01T08:00:00Z",
            ["ClosesUtc"] = "2026-12-15T08:00:00Z",
        };

        // Act
        LegacyQuizUpgrade.ApplySettings(legacy, survey);

        // Assert
        Assert.True(survey.Draft.Scoring.Enabled);
        Assert.Equal(70m, survey.Draft.Scoring.PassMarkPercent);
        Assert.Equal(30, survey.Draft.Timing.TimeLimitMinutes);
        Assert.Equal(3, survey.Access.Limits.Total);
        Assert.Equal(LimitCount.Started, survey.Access.Limits.Counts);
        Assert.Equal(new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc), survey.Access.OpensUtc);
        Assert.Equal(new DateTime(2026, 12, 15, 8, 0, 0, DateTimeKind.Utc), survey.Access.ClosesUtc);
    }

    [Fact]
    public void ApplySettings_MissingAttempts_KeepsTheOldDefaultOfOne_AndNullMeansNoLimit()
    {
        // Arrange
        var missing = new Survey();
        var unlimited = new Survey();

        // Act
        LegacyQuizUpgrade.ApplySettings(new JsonObject(), missing);
        LegacyQuizUpgrade.ApplySettings(new JsonObject { ["MaxAttempts"] = null }, unlimited);

        // Assert
        Assert.Equal(1, missing.Access.Limits.Total);
        Assert.Null(unlimited.Access.Limits.Total);
    }

    [Fact]
    public void SetSurveyId_PointsTheQuizAtItsSurvey_AndDropsTheOldSettings()
    {
        // Arrange
        var item = new ContentItem();
        ((JsonObject)item.Content)["QuizPart"] = new JsonObject { ["PassMark"] = 50m, ["QuestionIds"] = new JsonArray("q1"), ["GradingMethod"] = 1 };

        // Act
        var before = LegacyQuizUpgrade.NeedsSurvey(item);
        LegacyQuizUpgrade.SetSurveyId(item, "survey1");

        // Assert
        Assert.True(before);
        Assert.False(LegacyQuizUpgrade.NeedsSurvey(item));
        var part = LegacyQuizUpgrade.PartOf(item);
        Assert.Null(part["PassMark"]);
        Assert.Null(part["QuestionIds"]);
        Assert.Equal(1, part["GradingMethod"]!.GetValue<int>());
        Assert.True(item.TryGet<QuizPart>(out var quiz));
        Assert.Equal("survey1", quiz.SurveyId);
    }

    [Fact]
    public void RemovedContentTypes_ListTheBankAndThirteenQuestionTypes()
        => Assert.Equal(14, LegacyQuizUpgrade.RemovedContentTypes.Distinct().Count());
}
