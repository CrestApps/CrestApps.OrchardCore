using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// What the package panel, the runtime endpoint and the index show of a runtime state of either standard.
/// </summary>
public sealed class PackageStateSummaryTests
{
    [Fact]
    public void Scorm12State_IsSummarizedFromItsLessonStatus()
    {
        var state = new PackageRuntimeState { Cmi = new Scorm12Cmi { LessonStatus = "passed", ScoreRaw = "90", TotalTime = "0000:02:30.00", Exit = "suspend" } };

        var summary = PackageStateSummary.Of(state);

        Assert.Equal("passed", summary.Status);
        Assert.Equal(90m, summary.Score);
        Assert.Equal("0000:02:30.00", summary.TotalTime);
        Assert.True(summary.Resumes);
    }

    [Fact]
    public void Scorm12State_WithoutTime_HasNoTime()
    {
        var summary = PackageStateSummary.Of(new PackageRuntimeState { Cmi = new Scorm12Cmi() });

        Assert.Equal("not attempted", summary.Status);
        Assert.Null(summary.TotalTime);
        Assert.False(summary.Resumes);
    }

    [Theory]
    [InlineData("passed", "completed", "passed")]
    [InlineData("failed", "completed", "failed")]
    [InlineData("unknown", "completed", "completed")]
    [InlineData("unknown", "incomplete", "incomplete")]
    [InlineData("unknown", "not attempted", "not attempted")]
    [InlineData("unknown", "unknown", "unknown")]
    public void Scorm2004State_ShowsSuccessFirst_ThenCompletion(string success, string completion, string expected)
    {
        var state = Scorm2004(new Scorm2004Cmi { SuccessStatus = success, CompletionStatus = completion });

        Assert.Equal(expected, PackageStateSummary.Of(state).Status);
    }

    [Fact]
    public void Scorm2004State_UsesTheEvaluatedStatuses()
    {
        var state = Scorm2004(new Scorm2004Cmi
        {
            CompletionThreshold = "0.8",
            ProgressMeasure = "0.9",
            CompletionStatus = "incomplete",
            ScaledPassingScore = "0.6",
            ScoreScaled = "0.5",
            SuccessStatus = "passed",
        });

        Assert.Equal("failed", PackageStateSummary.Of(state).Status);
    }

    [Fact]
    public void Scorm2004State_ShowsTheScaledScoreAsAPercentage_AndTheTimeAsHoursMinutesSeconds()
    {
        var state = Scorm2004(new Scorm2004Cmi { ScoreScaled = "0.875", TotalTime = "PT1H2M3.5S", Exit = "suspend" });

        var summary = PackageStateSummary.Of(state);

        Assert.Equal(87.5m, summary.Score);
        Assert.Equal("1:02:03", summary.TotalTime);
        Assert.True(summary.Resumes);
    }

    [Fact]
    public void Scorm2004State_WithoutTime_HasNoTime()
    {
        Assert.Null(PackageStateSummary.Of(Scorm2004(new Scorm2004Cmi())).TotalTime);
    }

    [Fact]
    public void NoState_IsNotStarted()
    {
        var summary = PackageStateSummary.Of(null);

        Assert.Null(summary.Status);
        Assert.Null(summary.Score);
        Assert.False(summary.Resumes);
    }

    private static PackageRuntimeState Scorm2004(Scorm2004Cmi cmi)
        => new() { Standard = PackagesConstants.Standards.Scorm2004FourthEdition, Cmi = null, Cmi2004 = cmi };
}
