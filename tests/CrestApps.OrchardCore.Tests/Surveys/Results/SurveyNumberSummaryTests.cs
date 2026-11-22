using CrestApps.OrchardCore.Surveys.Core.Results;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The in-memory number summary (used for segments) keeps a bounded distribution, like the totals of the whole survey.
/// </summary>
public sealed class SurveyNumberSummaryTests
{
    [Fact]
    public void Summarize_ManyDistinctValues_KeepsTheLowestDistributionLimitValues()
    {
        // Arrange
        var values = Enumerable.Range(1, SurveyNumberSummary.DistributionLimit + 50).Select(value => (decimal)value).Reverse();

        // Act
        var statistics = SurveyNumberSummary.Summarize(values);

        // Assert
        Assert.Equal(SurveyNumberSummary.DistributionLimit + 50, statistics.Count);
        Assert.Equal(SurveyNumberSummary.DistributionLimit, statistics.Distribution.Count);
        Assert.Equal(1m, statistics.Distribution.Keys.Min());
        Assert.Equal(SurveyNumberSummary.DistributionLimit, statistics.Distribution.Keys.Max());
        Assert.Equal(SurveyNumberSummary.DistributionLimit + 50, statistics.Max);
    }
}
