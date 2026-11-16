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

        Assert.Equal(expected is null ? null : (decimal)expected, PackageResultMapper.ScorePercent(cmi));
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
}
