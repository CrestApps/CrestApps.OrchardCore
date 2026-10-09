using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportFilterValueBinderTests
{
    private static readonly ReportFilterDefinition[] _filters =
    [
        new() { Id = "region", Exposed = true },
        new() { Id = "placed", Exposed = true, Control = ReportFilterControl.DateRange },
        new() { Id = "name", Exposed = true },
        new() { Id = "fixed", Exposed = false },
    ];

    [Fact]
    public void Bind_WithoutTheAppliedMarker_KeepsTheDefaults()
    {
        // Act
        var values = ReportFilterValueBinder.Bind(Query(("f.region", "West")), _filters);

        // Assert
        Assert.Null(values);
    }

    [Fact]
    public void Bind_ReadsValuesRangesAndClearedFilters()
    {
        // Act
        var values = ReportFilterValueBinder.Bind(Query(
            ("applied", "1"),
            ("f.region", "West"),
            ("f.region", "East"),
            ("f.placed.from", "2026-01-01T00:00"),
            ("f.placed.to", "2026-01-31T23:59"),
            ("f.fixed", "Hacked")), _filters);

        // Assert
        Assert.Equal(["West", "East"], values["region"]);
        Assert.Equal(["2026-01-01T00:00", "2026-01-31T23:59:59"], values["placed"]);
        Assert.Empty(values["name"]);
        Assert.False(values.ContainsKey("fixed"));
    }

    [Fact]
    public void Bind_EmptyRange_TurnsTheFilterOff()
    {
        // Act
        var values = ReportFilterValueBinder.Bind(Query(("applied", "1"), ("f.placed.from", string.Empty), ("f.placed.to", string.Empty)), _filters);

        // Assert
        Assert.Empty(values["placed"]);
    }

    [Fact]
    public void ResolveControl_PicksAControlFromTheTypeAndOperator()
    {
        Assert.Equal(ReportFilterControl.Boolean, ReportDesignRunner.ResolveControl(new ReportFilterDefinition(), ReportDataType.Boolean));
        Assert.Equal(ReportFilterControl.DateRange, ReportDesignRunner.ResolveControl(new ReportFilterDefinition { Operator = ReportFilterOperator.Between }, ReportDataType.DateTime));
        Assert.Equal(ReportFilterControl.NumberRange, ReportDesignRunner.ResolveControl(new ReportFilterDefinition { Operator = ReportFilterOperator.Between }, ReportDataType.Decimal));
        Assert.Equal(ReportFilterControl.MultiSelect, ReportDesignRunner.ResolveControl(new ReportFilterDefinition { Operator = ReportFilterOperator.In }, ReportDataType.Text));
        Assert.Equal(ReportFilterControl.Select, ReportDesignRunner.ResolveControl(new ReportFilterDefinition { Operator = ReportFilterOperator.Equals }, ReportDataType.Text));
        Assert.Equal(ReportFilterControl.Text, ReportDesignRunner.ResolveControl(new ReportFilterDefinition { Operator = ReportFilterOperator.Contains }, ReportDataType.Text));
        Assert.Equal(ReportFilterControl.Select, ReportDesignRunner.ResolveControl(new ReportFilterDefinition { Control = ReportFilterControl.Select }, ReportDataType.Integer));
    }

    [Fact]
    public void Normalize_MakesAnExposedFilterUseTheOperatorItsControlNeeds()
    {
        // Arrange
        var query = new ReportQueryDefinition
        {
            DataSets = [null, new ReportDataSetReference { Alias = " c " }],
            Filters =
            [
                new ReportFilterDefinition { Id = "a", Exposed = true, Control = ReportFilterControl.DateRange, Operator = ReportFilterOperator.Equals },
                new ReportFilterDefinition { Id = "b", Exposed = true, Control = ReportFilterControl.MultiSelect, Operator = ReportFilterOperator.Contains },
                new ReportFilterDefinition { Id = "c", Exposed = false, Control = ReportFilterControl.DateRange, Operator = ReportFilterOperator.Equals, Values = null },
            ],
            Columns = null,
        };

        // Act
        ReportDesignNormalizer.Normalize(query);

        // Assert
        Assert.Equal("c", Assert.Single(query.DataSets).Alias);
        Assert.Equal(ReportFilterOperator.Between, query.Filters[0].Operator);
        Assert.Equal(ReportFilterOperator.In, query.Filters[1].Operator);
        Assert.Equal(ReportFilterOperator.Equals, query.Filters[2].Operator);
        Assert.Empty(query.Filters[2].Values);
        Assert.Empty(query.Columns);
    }

    [Fact]
    public void Normalize_ClampsVisualWidths()
    {
        // Arrange
        var visuals = new List<ReportVisualDefinition>
        {
            new() { Width = 40, ValueColumnIds = null },
            null,
        };

        // Act
        var normalized = ReportDesignNormalizer.Normalize(visuals);

        // Assert
        Assert.Equal(12, Assert.Single(normalized).Width);
        Assert.Empty(normalized[0].ValueColumnIds);
    }

    private static QueryCollection Query(params (string Key, string Value)[] entries)
    {
        return new QueryCollection(entries
            .GroupBy(entry => entry.Key)
            .ToDictionary(group => group.Key, group => new StringValues(group.Select(entry => entry.Value).ToArray())));
    }
}
