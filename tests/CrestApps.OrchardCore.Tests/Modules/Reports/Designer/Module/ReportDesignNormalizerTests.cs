using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportDesignNormalizerTests
{
    // The builder once added a second join for a data set when its fields loaded after the page had already drawn an
    // empty one, and the planner refuses a data set with two joins. Normalizing merges them so such designs run.
    [Fact]
    public void Normalize_MergesTwoJoinsOfTheSameDataSet()
    {
        // Arrange
        var query = new ReportQueryDefinition
        {
            Joins =
            [
                new ReportJoinDefinition { Alias = "Customer", Type = DataJoinType.Left, Conditions = [new ReportJoinCondition()] },
                new ReportJoinDefinition
                {
                    Alias = "customer",
                    Type = DataJoinType.Inner,
                    Conditions =
                    [
                        new ReportJoinCondition { LeftField = "Users.UserId", RightField = "Customer.Owner" },
                        new ReportJoinCondition { LeftField = "Users.UserId", RightField = "Customer.Owner" },
                    ],
                },
                new ReportJoinDefinition { Alias = "Order", Conditions = [new ReportJoinCondition { LeftField = "Customer.ContentItemId", RightField = "Order.Customer" }] },
            ],
        };

        // Act
        var normalized = ReportDesignNormalizer.Normalize(query);

        // Assert
        Assert.Equal(["Customer", "Order"], normalized.Joins.Select(join => join.Alias));
        var customer = normalized.Joins[0];
        Assert.Equal(DataJoinType.Left, customer.Type);
        var condition = Assert.Single(customer.Conditions);
        Assert.Equal("Users.UserId", condition.LeftField);
        Assert.Equal("Customer.Owner", condition.RightField);
    }

    [Fact]
    public void Normalize_GivesARecentPeriodFilterTheLastDaysOperator()
    {
        // Arrange
        var query = new ReportQueryDefinition
        {
            Filters = [new ReportFilterDefinition { Id = "f1", Field = "A.CreatedUtc", Exposed = true, Control = ReportFilterControl.RelativeDate, Operator = DataFilterOperator.Between, Values = ["30"] }],
        };

        // Act
        var filter = Assert.Single(ReportDesignNormalizer.Normalize(query).Filters);

        // Assert
        Assert.Equal(DataFilterOperator.InLastDays, filter.Operator);
    }

    [Fact]
    public void Normalize_KeepsAnIncompleteJoinWhenItHasNothingElse()
    {
        // Arrange
        var query = new ReportQueryDefinition
        {
            Joins = [new ReportJoinDefinition { Alias = "Customer", Conditions = [new ReportJoinCondition { LeftField = "Users.UserId" }] }],
        };

        // Act
        var normalized = ReportDesignNormalizer.Normalize(query);

        // Assert
        Assert.Equal("Users.UserId", Assert.Single(Assert.Single(normalized.Joins).Conditions).LeftField);
    }

    [Fact]
    public void Normalize_Queries_DropsUnnamedAndRepeatedOnesAndNormalizesEachQuery()
    {
        // Arrange
        List<ReportNamedQuery> queries =
        [
            null,
            new() { Id = " q1 ", Name = "  By day ", Query = null },
            new() { Id = "q1", Name = "Again" },
            new() { Id = "", Name = "No id" },
        ];

        // Act
        var normalized = ReportDesignNormalizer.Normalize(queries);

        // Assert
        var query = Assert.Single(normalized);
        Assert.Equal("q1", query.Id);
        Assert.Equal("By day", query.Name);
        Assert.NotNull(query.Query);
        Assert.Empty(query.Query.Columns);
    }

    [Fact]
    public void Normalize_Visuals_GivesEachVisualItsOwnIdentifier()
    {
        // Arrange
        List<ReportVisualDefinition> visuals =
        [
            new() { Id = "v1" },
            new() { Id = null, QueryId = " " },
            new() { Id = "v1", QueryId = " q1 " },
        ];

        // Act
        var normalized = ReportDesignNormalizer.Normalize(visuals);

        // Assert
        Assert.Equal(["v1", "v2", "v3"], normalized.Select(visual => visual.Id));
        Assert.Equal([null, null, "q1"], normalized.Select(visual => visual.QueryId));
    }

    [Fact]
    public void Normalize_Containers_DropsUnnamedAndRepeatedOnesAndClampsTheirSize()
    {
        // Arrange
        List<ReportContainerDefinition> containers =
        [
            null,
            new() { Id = " scorecard ", Title = "  Scorecard ", Width = 40, Height = -5 },
            new() { Id = "scorecard", Title = "Again" },
            new() { Id = " ", Title = "No id" },
            new() { Id = "calls", Width = 0, Height = 5000 },
        ];

        // Act
        var normalized = ReportDesignNormalizer.Normalize(containers);

        // Assert
        Assert.Equal(["scorecard", "calls"], normalized.Select(container => container.Id));
        Assert.Equal("Scorecard", normalized[0].Title);
        Assert.Equal(12, normalized[0].Width);
        Assert.Equal(0, normalized[0].Height);
        Assert.Equal(1, normalized[1].Width);
        Assert.Equal(ReportContainerDefinition.MaxHeight, normalized[1].Height);
    }
}
