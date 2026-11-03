using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

public sealed class ReportSelectionKeysTests
{
    [Fact]
    public void ToKey_WritesValuesInTheInvariantCulture()
    {
        Assert.Equal(string.Empty, ReportSelectionKeys.ToKey(null));
        Assert.Equal("Alexis Romero", ReportSelectionKeys.ToKey("Alexis Romero"));
        Assert.Equal("1234.5", ReportSelectionKeys.ToKey(1234.5m));
        Assert.Equal("true", ReportSelectionKeys.ToKey(true));
        Assert.Equal("2026-10-26", ReportSelectionKeys.ToKey(new DateTime(2026, 10, 26)));
        Assert.Equal("2026-10-26T14:30:00", ReportSelectionKeys.ToKey(new DateTime(2026, 10, 26, 14, 30, 0)));
    }

    [Fact]
    public void ToFilter_OneValue_KeepsTheRowsEqualToIt()
    {
        // Act
        var filter = ReportSelectionKeys.ToFilter("sel.agents", "Calls.Agent", DataFieldTransform.None, ["Alexis Romero"]);

        // Assert
        Assert.Equal("sel.agents", filter.Id);
        Assert.Equal("Calls.Agent", filter.Field);
        Assert.Equal(ReportFilterStage.Rows, filter.Stage);
        Assert.Equal(DataFilterOperator.Equals, filter.Operator);
        Assert.Equal(["Alexis Romero"], filter.Values);
        Assert.False(filter.Exposed);
    }

    [Fact]
    public void ToFilter_SeveralValues_KeepsTheRowsEqualToAnyOfThem()
    {
        // Act
        var filter = ReportSelectionKeys.ToFilter("sel.agents", "Calls.Agent", DataFieldTransform.Upper, ["A", "B", "A", string.Empty]);

        // Assert
        Assert.Equal(DataFilterOperator.In, filter.Operator);
        Assert.Equal(["A", "B"], filter.Values);
    }

    [Fact]
    public void ToFilter_OnlyAMissingValue_KeepsTheRowsWithoutOne()
    {
        // Act
        var filter = ReportSelectionKeys.ToFilter("sel.agents", "Calls.Agent", DataFieldTransform.None, [string.Empty]);

        // Assert
        Assert.Equal(DataFilterOperator.IsEmpty, filter.Operator);
        Assert.Empty(filter.Values);
    }

    [Theory]
    [InlineData(DataFieldTransform.Day, "2026-10-26", "2026-10-26", "2026-10-26")]
    [InlineData(DataFieldTransform.Week, "2026-10-26", "2026-10-26", "2026-11-01")]
    [InlineData(DataFieldTransform.Month, "2026-02-01", "2026-02-01", "2026-02-28")]
    [InlineData(DataFieldTransform.Year, "2026", "2026-01-01", "2026-12-31")]
    [InlineData(DataFieldTransform.Hour, "2026-10-26T14:00:00", "2026-10-26T14:00", "2026-10-26T14:59:59")]
    public void ToFilter_ADateKeptToAPeriod_KeepsTheRowsOfThatPeriod(DataFieldTransform transform, string key, string from, string to)
    {
        // Act
        var filter = ReportSelectionKeys.ToFilter("sel.trend", "Calls.StartedUtc", transform, [key]);

        // Assert
        Assert.Equal(DataFilterOperator.Between, filter.Operator);
        Assert.Equal([from, to], filter.Values);
    }

    [Fact]
    public void ToFilter_SeveralDays_KeepsTheRowsFromTheFirstToTheLast()
    {
        // Act
        var filter = ReportSelectionKeys.ToFilter("sel.trend", "Calls.StartedUtc", DataFieldTransform.Day, ["2026-10-21", "2026-10-15", "2026-10-17"]);

        // Assert
        Assert.Equal(["2026-10-15", "2026-10-21"], filter.Values);
    }

    [Theory]
    [InlineData(DataFieldTransform.DayOfWeek)]
    [InlineData(DataFieldTransform.MonthOfYear)]
    [InlineData(DataFieldTransform.Quarter)]
    [InlineData(DataFieldTransform.Length)]
    [InlineData(DataFieldTransform.Round)]
    public void ToFilter_AValueThatDoesNotMapBackToItsField_FiltersNothing(DataFieldTransform transform)
    {
        Assert.False(ReportSelectionKeys.Supports(transform));
        Assert.Null(ReportSelectionKeys.ToFilter("sel.trend", "Calls.StartedUtc", transform, ["3"]));
    }

    [Fact]
    public void ToFilter_NoValues_FiltersNothing()
    {
        Assert.Null(ReportSelectionKeys.ToFilter("sel.agents", "Calls.Agent", DataFieldTransform.None, []));
        Assert.Null(ReportSelectionKeys.ToFilter("sel.agents", null, DataFieldTransform.None, ["A"]));
    }
}
