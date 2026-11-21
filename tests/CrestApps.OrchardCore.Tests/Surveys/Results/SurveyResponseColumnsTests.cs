using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

public sealed class SurveyResponseColumnsTests
{
    [Fact]
    public void Build_JoinsVersionsByQuestionId_WithTheNewestNames()
    {
        // Arrange: q1 was renamed in version 2, q2 was removed, q3 was added.
        var version1 = ResultsSqliteStore.Version("v1", "s", 1, ResultsSqliteStore.NumberQuestion("q1", "rating"), ResultsSqliteStore.NumberQuestion("q2", "age"));
        var version2 = ResultsSqliteStore.Version("v2", "s", 2, ResultsSqliteStore.NumberQuestion("q1", "satisfaction"), ResultsSqliteStore.NumberQuestion("q3", "region"));

        // Act
        var columns = SurveyResponseColumns.Build([version1, version2], Registry());

        // Assert
        Assert.Equal(["satisfaction", "region", "age"], columns.Columns.Select(column => column.Key));
    }

    [Fact]
    public void Build_AnOldNameAnotherQuestionReuses_GetsANumber()
    {
        // Arrange: version 2 gave the name "age" to a new question.
        var version1 = ResultsSqliteStore.Version("v1", "s", 1, ResultsSqliteStore.NumberQuestion("q1", "age"));
        var version2 = ResultsSqliteStore.Version("v2", "s", 2, ResultsSqliteStore.NumberQuestion("q9", "age"));

        // Act
        var columns = SurveyResponseColumns.Build([version1, version2], Registry());

        // Assert
        Assert.Equal(["age", "age_2"], columns.Columns.Select(column => column.Key));
    }

    [Fact]
    public void Write_AnOldResponse_WritesUnderTheNewestName()
    {
        // Arrange
        var version1 = ResultsSqliteStore.Version("v1", "s", 1, ResultsSqliteStore.NumberQuestion("q1", "rating"));
        var version2 = ResultsSqliteStore.Version("v2", "s", 2, ResultsSqliteStore.NumberQuestion("q1", "satisfaction"));
        var columns = SurveyResponseColumns.Build([version1, version2], Registry());
        var response = ResultsSqliteStore.Response("r1", version1, SurveyResponseStatus.Completed, new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), ("q1", new SurveyAnswer { Number = 4 }));
        var row = new Dictionary<string, object>(StringComparer.Ordinal);

        // Act
        columns.Write(response, version1.Definition, row, new SurveyFormatContext());

        // Assert
        Assert.Equal(4m, row["satisfaction"]);
        Assert.False(row.ContainsKey("rating"));
    }

    private static SurveyControlRegistry Registry()
    {
        var options = new SurveyControlOptions();
        options.Controls[ResultsTestControlHandler.Name] = new SurveyControlDescriptor
        {
            Name = ResultsTestControlHandler.Name,
            Capabilities = SurveyControlCapabilities.Answerable,
        };

        return new SurveyControlRegistry(Options.Create(options), [new ResultsTestControlHandler()], [], []);
    }
}
