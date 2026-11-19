using CrestApps.OrchardCore.ServiceLevels.Core.Models;
using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.Tests.ServiceLevels;

/// <summary>
/// The policy editor shows the targets as a grid of metrics by priority, with a last row for every other priority,
/// and reads it back without losing a target the grid does not show.
/// </summary>
public sealed class ServiceLevelTargetGridTests
{
    private static readonly ServiceLevelOption[] _metrics =
    [
        new(ServiceLevelMetrics.FirstResponse, "First response"),
        new(ServiceLevelMetrics.Resolution, "Resolution"),
    ];

    private static readonly ServiceLevelOption[] _priorities =
    [
        new("Urgent", "Urgent"),
        new("Normal", "Normal"),
    ];

    [Fact]
    public void BuildCells_LaysTheTargetsOutRowByRow_WithAnyPriorityLast()
    {
        ServiceLevelTarget[] targets =
        [
            new() { Metric = ServiceLevelMetrics.FirstResponse, Priority = "urgent", Minutes = 30 },
            new() { Metric = ServiceLevelMetrics.Resolution, Minutes = 2880 },
        ];

        var cells = ServiceLevelTargetGrid.BuildCells(targets, _metrics, _priorities);

        Assert.Equal(6, cells.Count);
        Assert.Equal(("FirstResponse", "Urgent", (int?)30), (cells[0].Metric, cells[0].Priority, cells[0].Minutes));
        Assert.Null(cells[1].Minutes);
        Assert.Equal(string.Empty, cells[4].Priority);
        Assert.Equal(2880, cells[5].Minutes);
    }

    [Fact]
    public void ApplyCells_DropsEmptyCells_AndKeepsTargetsTheGridDoesNotShow()
    {
        ServiceLevelTarget[] existing =
        [
            new() { Metric = ServiceLevelMetrics.FirstResponse, Priority = "Urgent", Minutes = 30 },
            new() { Metric = "Callback", Priority = "Urgent", Minutes = 15 },
            new() { Metric = ServiceLevelMetrics.Resolution, Priority = "Retired", Minutes = 99 },
        ];
        ServiceLevelTargetCell[] cells =
        [
            new() { Metric = ServiceLevelMetrics.FirstResponse, Priority = "Urgent", Minutes = 45 },
            new() { Metric = ServiceLevelMetrics.Resolution, Priority = "Urgent", Minutes = null },
            new() { Metric = ServiceLevelMetrics.Resolution, Priority = string.Empty, Minutes = 600 },
            new() { Metric = "Injected", Priority = string.Empty, Minutes = 5 },
        ];

        var targets = ServiceLevelTargetGrid.ApplyCells(existing, cells, _metrics, _priorities);

        Assert.Equal(4, targets.Count);
        Assert.Contains(targets, target => target.Metric == "Callback" && target.Minutes == 15);
        Assert.Contains(targets, target => target.Priority == "Retired" && target.Minutes == 99);
        Assert.Contains(targets, target => target.Metric == ServiceLevelMetrics.FirstResponse && target.Minutes == 45);
        Assert.Contains(targets, target => target.Metric == ServiceLevelMetrics.Resolution && target.Priority is null && target.Minutes == 600);
        Assert.DoesNotContain(targets, target => target.Metric == "Injected");
    }
}
