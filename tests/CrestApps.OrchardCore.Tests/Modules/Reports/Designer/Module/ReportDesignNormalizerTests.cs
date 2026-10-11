using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;

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
                new ReportJoinDefinition { Alias = "Customer", Type = ReportJoinType.Left, Conditions = [new ReportJoinCondition()] },
                new ReportJoinDefinition
                {
                    Alias = "customer",
                    Type = ReportJoinType.Inner,
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
        Assert.Equal(ReportJoinType.Left, customer.Type);
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
            Filters = [new ReportFilterDefinition { Id = "f1", Field = "A.CreatedUtc", Exposed = true, Control = ReportFilterControl.RelativeDate, Operator = ReportFilterOperator.Between, Values = ["30"] }],
        };

        // Act
        var filter = Assert.Single(ReportDesignNormalizer.Normalize(query).Filters);

        // Assert
        Assert.Equal(ReportFilterOperator.InLastDays, filter.Operator);
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
}
