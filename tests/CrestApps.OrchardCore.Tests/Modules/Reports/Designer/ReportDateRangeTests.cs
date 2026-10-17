using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

public sealed class ReportDateRangeTests
{
    private static readonly DateTime _may1 = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void For_CombinesBoundsOfTheField_IntoTheNarrowestInclusiveRange()
    {
        // Arrange
        var conditions = new[]
        {
            new ReportDataCondition { Field = "CreatedUtc", Operator = ReportFilterOperator.GreaterThan, Values = [_may1] },
            new ReportDataCondition { Field = "CreatedUtc", Operator = ReportFilterOperator.Between, Values = [_may1.AddDays(2), _may1.AddDays(9)] },
            new ReportDataCondition { Field = "CreatedUtc", Operator = ReportFilterOperator.LessThanOrEqual, Values = [_may1.AddDays(5)] },
            new ReportDataCondition { Field = "OtherUtc", Operator = ReportFilterOperator.LessThan, Values = [_may1] },
        };

        // Act
        var (from, to) = ReportDateRange.For(conditions, "CreatedUtc");

        // Assert
        Assert.Equal(_may1.AddDays(2), from);
        Assert.Equal(_may1.AddDays(5), to);
    }

    [Fact]
    public void For_IgnoresConditionsItCannotTurnIntoARange()
    {
        // Arrange
        var conditions = new[]
        {
            new ReportDataCondition { Field = "CreatedUtc", Operator = ReportFilterOperator.InLastDays, Values = [7L] },
            new ReportDataCondition { Field = "CreatedUtc", Operator = ReportFilterOperator.GreaterThan, Values = ["not a date"] },
            new ReportDataCondition { Field = "CreatedUtc", Operator = ReportFilterOperator.NotEquals, Values = [_may1] },
        };

        // Act
        var (from, to) = ReportDateRange.For(conditions, "CreatedUtc");

        // Assert
        Assert.Null(from);
        Assert.Null(to);
    }
}
