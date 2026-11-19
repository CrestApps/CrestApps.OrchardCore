using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// What a work order shows of its service levels: a metric with no timer left to read keeps its due time and breach
/// history, and a work order moved back out of Completed counts its resolution again.
/// </summary>
public sealed class WorkOrderServiceLevelsTests
{
    private static readonly DateTime _now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AMetricWithNoTimerLeft_KeepsItsDueTimeAndBreachHistory()
    {
        // Arrange: the response was breached; its timer is gone (cancelled), the resolution never had one.
        var tracker = new Mock<IServiceLevelTracker>();
        tracker.Setup(value => value.GetTimersAsync(It.IsAny<RecordRef>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ServiceLevelTimer { Metric = ServiceLevelMetrics.FirstResponse, State = ServiceLevelTimerState.Cancelled, StartedUtc = _now }]);
        var serviceLevels = new WorkOrderServiceLevels(tracker.Object, Mock.Of<IServiceLevelPolicyManager>());
        var workOrder = new WorkOrder
        {
            ItemId = "wo1",
            Sla = new WorkOrderSla
            {
                PolicyId = "p1",
                RespondByUtc = _now.AddHours(-1),
                ResponseWarned = true,
                ResponseBreached = true,
                ResolveByUtc = _now.AddHours(6),
            },
        };

        // Act
        await serviceLevels.RefreshAsync(workOrder, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now.AddHours(-1), workOrder.Sla.RespondByUtc);
        Assert.True(workOrder.Sla.ResponseBreached);
        Assert.True(workOrder.Sla.ResponseWarned);
        Assert.Equal(_now.AddHours(6), workOrder.Sla.ResolveByUtc);
    }

    [Fact]
    public async Task AWorkOrderMovedBackToInProgress_CountsItsResolutionAgain()
    {
        // Arrange
        var tracker = new Mock<IServiceLevelTracker>();
        tracker.Setup(value => value.GetTimersAsync(It.IsAny<RecordRef>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var serviceLevels = new WorkOrderServiceLevels(tracker.Object, Mock.Of<IServiceLevelPolicyManager>());
        var workOrder = new WorkOrder
        {
            ItemId = "wo1",
            Status = WorkOrderStatus.InProgress,
            Sla = new WorkOrderSla { PolicyId = "p1", FirstResponseUtc = _now.AddHours(-5) },
        };

        // Act
        await serviceLevels.MovedAsync(workOrder, subStatus: null, _now, TestContext.Current.CancellationToken);

        // Assert
        tracker.Verify(value => value.ReopenAsync(WorkOrderServiceLevels.SubjectOf(workOrder), ServiceLevelMetrics.Resolution, It.IsAny<CancellationToken>()), Times.Once);
        tracker.Verify(value => value.ResumeAsync(WorkOrderServiceLevels.SubjectOf(workOrder), null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
