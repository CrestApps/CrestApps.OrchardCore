using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// The conditions passed to data sources must never drop a row the report's own filter keeps.
/// </summary>
public sealed class ReportFilterConditionTests
{
    // The tenant is two hours behind UTC in these tests.
    private static readonly Func<DateTime, DateTime> _toUtc = local => DateTime.SpecifyKind(local.AddHours(2), DateTimeKind.Utc);
    private static readonly DateTime _today = new(2026, 5, 20, 15, 30, 0);

    [Fact]
    public void InLastDays_OnADateTime_PassesTheWholeDaysDownAsAUtcRange()
    {
        // Act
        var condition = ReportFilterPredicates.BuildCondition("CreatedUtc", ReportFilterOperator.InLastDays, ReportDataType.DateTime, ["30"], _toUtc, _today);

        // Assert
        Assert.Equal(ReportFilterOperator.Between, condition.Operator);
        Assert.Equal(new DateTime(2026, 4, 21, 2, 0, 0, DateTimeKind.Utc), condition.Values[0]);
        Assert.Equal(new DateTime(2026, 5, 21, 2, 0, 0, DateTimeKind.Utc), condition.Values[1]);
    }

    [Fact]
    public void InLastDays_KeepsEveryRowTheFilterKeeps()
    {
        // Arrange
        var filter = ReportFilterPredicates.Build(ReportFilterOperator.InLastDays, ReportDataType.DateTime, ["7"], _today);
        var condition = ReportFilterPredicates.BuildCondition("CreatedUtc", ReportFilterOperator.InLastDays, ReportDataType.DateTime, ["7"], _toUtc, _today);
        var from = (DateTime)condition.Values[0];
        var to = (DateTime)condition.Values[1];

        // Act & Assert: every local minute of the last eight days the filter keeps is inside the UTC range.
        for (var local = _today.Date.AddDays(-8); local < _today.Date.AddDays(2); local = local.AddMinutes(30))
        {
            if (filter(local))
            {
                var utc = _toUtc(local);
                Assert.True(utc >= from && utc <= to, $"{local:O} is kept by the filter but outside the passed-down range.");
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("not a number")]
    public void InLastDays_WithoutAPeriod_PassesNothingDown(string days)
    {
        // Act
        var condition = ReportFilterPredicates.BuildCondition("CreatedUtc", ReportFilterOperator.InLastDays, ReportDataType.DateTime, [days], _toUtc, _today);

        // Assert
        Assert.Null(condition);
    }

    [Fact]
    public void InNextDays_OnADate_PassesTheDaysDown()
    {
        // Act
        var condition = ReportFilterPredicates.BuildCondition("Due", ReportFilterOperator.InNextDays, ReportDataType.Date, ["3"], _toUtc, _today);

        // Assert
        Assert.Equal(new DateTime(2026, 5, 20), condition.Values[0]);
        Assert.Equal(new DateTime(2026, 5, 23), condition.Values[1]);
    }
}
