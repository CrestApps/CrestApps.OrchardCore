using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Review V-L12: a quiz's pass follows the attempt its grading method picks, decided against that attempt's own version,
/// never against the version of the attempt that triggered the change.
/// </summary>
public sealed class QuizResultProviderPassTests
{
    private static readonly DateTime _day = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

    // v2 raised the pass mark to 70.
    private static readonly SurveyDefinition _v2 = new() { Scoring = new SurveyScoringSettings { Enabled = true, PassMarkPercent = 70 } };

    [Fact]
    public void Highest_KeepsTheAttemptThatPassedOnItsOwnVersion_WhenALaterVersionRaisedThePassMark()
    {
        // Arrange: attempt 1 on v1 (pass mark 60) scored 65 and passed; attempt 2 on v2 scored 50 and failed.
        var attempts = new[] { Attempt("a1", 65, passed: true, 0), Attempt("a2", 50, passed: false, 1) };

        // Act
        var passed = QuizResultProvider.Passed(attempts, GradingMethod.Highest, _v2, 65);

        // Assert
        Assert.True(passed);
    }

    [Fact]
    public void Latest_TakesTheLatestAttemptsOwnPass()
    {
        var attempts = new[] { Attempt("a1", 65, passed: true, 0), Attempt("a2", 50, passed: false, 1) };

        Assert.False(QuizResultProvider.Passed(attempts, GradingMethod.Latest, _v2, 50));
    }

    [Fact]
    public void Average_IsDecidedByTheOnePassRule_AgainstTheGivenVersion()
    {
        var attempts = new[] { Attempt("a1", 80, passed: true, 0), Attempt("a2", 50, passed: false, 1) };

        Assert.False(QuizResultProvider.Passed(attempts, GradingMethod.Average, _v2, 65));
    }

    [Fact]
    public void AnAttemptWithNoPassOfItsOwn_IsDecidedByTheOnePassRule()
    {
        var attempts = new[] { Attempt("a1", 75, passed: null, 0) };

        Assert.True(QuizResultProvider.Passed(attempts, GradingMethod.Highest, _v2, 75));
    }

    [Fact]
    public void AMissingVersion_StillTakesThePickedAttemptsPass()
    {
        // A delete whose response's version is gone gives no definition.
        var attempts = new[] { Attempt("a1", 65, passed: true, 0) };

        Assert.True(QuizResultProvider.Passed(attempts, GradingMethod.Highest, null, 65));
    }

    private static SurveyResponse Attempt(string id, decimal percent, bool? passed, int order)
        => new()
        {
            ItemId = id,
            Status = SurveyResponseStatus.Completed,
            Grading = GradingStatus.Graded,
            Percent = percent,
            Passed = passed,
            StartedUtc = _day.AddHours(order),
            CompletedUtc = _day.AddHours(order).AddMinutes(30),
        };
}
