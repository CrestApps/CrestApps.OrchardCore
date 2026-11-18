using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class PackageResultMapperTests
{
    [Theory]
    [InlineData("", "", "", null)]
    [InlineData("85", "", "", 85d)]
    [InlineData("85.555", "", "", 85.56d)]
    [InlineData("8", "", "10", 80d)]
    [InlineData("8", "0", "10", 80d)]
    [InlineData("30", "20", "40", 50d)]
    [InlineData("5", "0", "0", 5d)]
    [InlineData("50", "60", "100", 0d)]
    public void ScorePercent_ScalesTheRawScoreToItsRange(string raw, string min, string max, double? expected)
    {
        var cmi = new Scorm12Cmi { ScoreRaw = raw, ScoreMin = min, ScoreMax = max };

        Assert.Equal(expected is null ? null : Math.Round((decimal)expected, 2), PackageResultMapper.ScorePercent(cmi));
    }

    [Theory]
    [InlineData("completed", true, null)]
    [InlineData("passed", true, true)]
    [InlineData("failed", false, false)]
    [InlineData("incomplete", false, null)]
    [InlineData("browsed", false, null)]
    [InlineData("not attempted", false, null)]
    public void Map_ReportsCompletionAndPass_AlwaysSelfReported(string status, bool complete, bool? passed)
    {
        var result = PackageResultMapper.Map(new Scorm12Cmi { LessonStatus = status, ScoreRaw = "72" }, "package1", 3);

        Assert.Equal("package1", result.ItemId);
        Assert.Equal(complete, result.ReportedComplete);
        Assert.Equal(passed, result.Passed);
        Assert.Equal(72m, result.Score);
        Assert.Equal(3, result.Attempts);
        Assert.True(result.SelfReported);
        Assert.Null(result.RecordedBy);
        Assert.False(result.Graded);
    }

    [Theory]
    [InlineData("0.85", null, null, null, 85d)]
    [InlineData("1", null, null, null, 100d)]
    [InlineData("-0.2", null, null, null, 0d)]
    [InlineData("0.12345", null, null, null, 12.35d)]
    [InlineData("0.5", "90", null, null, 50d)]
    [InlineData(null, "8", "0", "10", 80d)]
    [InlineData(null, "30", "20", "40", 50d)]
    [InlineData(null, "85", null, null, 85d)]
    [InlineData(null, "250", null, null, 100d)]
    [InlineData(null, null, "0", "10", null)]
    public void ScorePercent2004_IsTheScaledScoreTimes100_ElseTheRawScoreInItsRange(string scaled, string raw, string min, string max, double? expected)
    {
        var cmi = new Scorm2004Cmi { ScoreScaled = scaled, ScoreRaw = raw, ScoreMin = min, ScoreMax = max };

        Assert.Equal(expected is null ? null : Math.Round((decimal)expected, 2), PackageResultMapper.ScorePercent(cmi));
    }

    [Theory]
    [InlineData("completed", "unknown", null, null, null, null, true, null)]
    [InlineData("incomplete", "passed", null, null, null, null, false, true)]
    [InlineData("unknown", "failed", null, null, null, null, false, false)]
    [InlineData("not attempted", "unknown", null, null, null, null, false, null)]
    [InlineData("incomplete", "unknown", "0.8", "0.9", null, null, true, null)]
    [InlineData("completed", "unknown", "0.8", null, null, null, false, null)]
    [InlineData("unknown", "unknown", null, null, "0.6", "0.7", false, true)]
    [InlineData("unknown", "passed", null, null, "0.6", "0.5", false, false)]
    public void Map2004_ReportsTheEvaluatedCompletionAndSuccess_AlwaysSelfReported(string completion, string success, string threshold, string progress, string passing, string scaled, bool complete, bool? passed)
    {
        var cmi = new Scorm2004Cmi
        {
            CompletionStatus = completion,
            SuccessStatus = success,
            CompletionThreshold = threshold,
            ProgressMeasure = progress,
            ScaledPassingScore = passing,
            ScoreScaled = scaled,
        };

        var result = PackageResultMapper.Map(cmi, "package2004", 2);

        Assert.Equal("package2004", result.ItemId);
        Assert.Equal(complete, result.ReportedComplete);
        Assert.Equal(passed, result.Passed);
        Assert.Equal(2, result.Attempts);
        Assert.True(result.SelfReported);
        Assert.Null(result.RecordedBy);
    }
}
