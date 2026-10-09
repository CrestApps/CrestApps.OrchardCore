using System.Globalization;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.ContentManagement.Records;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Contents;

public sealed class ContentReportConditionTranslatorTests
{
    private static readonly DateTime _march1 = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ReportFilterOperator.GreaterThanOrEqual, "2026-02-28", false)]
    [InlineData(ReportFilterOperator.GreaterThanOrEqual, "2026-03-01", true)]
    [InlineData(ReportFilterOperator.GreaterThan, "2026-03-01", true)]
    [InlineData(ReportFilterOperator.LessThanOrEqual, "2026-03-01", true)]
    [InlineData(ReportFilterOperator.LessThanOrEqual, "2026-03-02", false)]
    [InlineData(ReportFilterOperator.LessThan, "2026-03-01", true)]
    public void Translate_DateBounds_AreInclusive(ReportFilterOperator filterOperator, string createdUtc, bool kept)
    {
        // Arrange
        var condition = Condition("CreatedUtc", filterOperator, _march1);

        // Act
        var predicates = ContentReportConditionTranslator.Translate([condition]);

        // Assert
        var predicate = Assert.Single(predicates).Compile();
        Assert.Equal(kept, predicate(new ContentItemIndex { CreatedUtc = DateTime.Parse(createdUtc, CultureInfo.InvariantCulture) }));
    }

    [Fact]
    public void Translate_BetweenWithAnOpenBound_AddsOnlyTheGivenBound()
    {
        // Arrange
        var condition = Condition("ModifiedUtc", ReportFilterOperator.Between, null, _march1);

        // Act
        var predicates = ContentReportConditionTranslator.Translate([condition]);

        // Assert
        var predicate = Assert.Single(predicates).Compile();
        Assert.True(predicate(new ContentItemIndex { ModifiedUtc = _march1.AddDays(-30) }));
        Assert.False(predicate(new ContentItemIndex { ModifiedUtc = _march1.AddDays(1) }));
    }

    [Fact]
    public void Translate_IsNotEmpty_TestsForNotNull()
    {
        // Arrange
        var condition = Condition("DisplayText", ReportFilterOperator.IsNotEmpty);

        // Act
        var predicate = Assert.Single(ContentReportConditionTranslator.Translate([condition])).Compile();

        // Assert
        Assert.True(predicate(new ContentItemIndex { DisplayText = " " }));
        Assert.False(predicate(new ContentItemIndex { DisplayText = null }));
    }

    [Theory]
    [InlineData("ContentItemId", ReportFilterOperator.In)]
    [InlineData("DisplayText", ReportFilterOperator.In)]
    [InlineData("DisplayText", ReportFilterOperator.Contains)]
    [InlineData("Owner", ReportFilterOperator.In)]
    [InlineData("Author", ReportFilterOperator.StartsWith)]
    [InlineData("Customer.Email", ReportFilterOperator.In)]
    [InlineData("CreatedUtc", ReportFilterOperator.In)]
    public void Translate_TextAndUnknownConditions_AreNotPushedDown(string field, ReportFilterOperator filterOperator)
    {
        // Arrange
        var condition = Condition(field, filterOperator, "Value");

        // Act
        var predicates = ContentReportConditionTranslator.Translate([condition]);

        // Assert
        Assert.Empty(predicates);
    }

    [Fact]
    public void Translate_WhenTheBoundIsNotADate_SkipsTheCondition()
    {
        // Arrange
        var condition = Condition("PublishedUtc", ReportFilterOperator.GreaterThanOrEqual, "2026-03-01");

        // Act
        var predicates = ContentReportConditionTranslator.Translate([condition, null]);

        // Assert
        Assert.Empty(predicates);
    }

    private static ReportDataCondition Condition(string field, ReportFilterOperator filterOperator, params object[] values)
    {
        return new ReportDataCondition
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };
    }
}
