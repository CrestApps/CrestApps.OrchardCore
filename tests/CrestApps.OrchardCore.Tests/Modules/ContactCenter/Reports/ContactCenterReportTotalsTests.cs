using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

public sealed class ContactCenterReportTotalsTests
{
    [Fact]
    public void AgentProductivityGrandTotal_RecomputesWeightedAveragesFromRawTotals()
    {
        // Arrange
        var rows = new[]
        {
            new AgentProductivityRow
            {
                InteractionsHandled = 1,
                InboundHandled = 1,
                TotalTalkTimeSeconds = 100,
                AverageHandleTimeSeconds = 100,
            },
            new AgentProductivityRow
            {
                InteractionsHandled = 9,
                OutboundHandled = 9,
                TotalTalkTimeSeconds = 90,
                AverageHandleTimeSeconds = 10,
            },
        };

        // Act
        var total = AgentProductivityReportProvider.CreateGrandTotalRow(rows, "All agents");

        // Assert
        Assert.Equal(ReportRowKind.GrandTotal, total.Kind);
        Assert.Equal("10", total.Cells[1]);
        Assert.Equal("19s", total.Cells[4]);
    }

    [Fact]
    public void EnterprisePerformanceGrandTotal_RecomputesRatesAndAveragesFromInteractions()
    {
        // Arrange
        var startedUtc = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        var interactions = new[]
        {
            CreateInteraction(startedUtc, answeredAfterSeconds: 10, endedAfterSeconds: 110),
            CreateInteraction(startedUtc, answeredAfterSeconds: 20, endedAfterSeconds: 40),
            CreateInteraction(startedUtc, answeredAfterSeconds: null, endedAfterSeconds: 30),
        };

        // Act
        var total = EnterpriseInteractionReportProvider.CreatePerformanceRow(
            "Grand total",
            EnterpriseInteractionReportProvider.Aggregate(interactions),
            ReportRowKind.GrandTotal);

        // Assert
        Assert.Equal(ReportRowKind.GrandTotal, total.Kind);
        Assert.Equal("66.7%", total.Cells[5]);
        Assert.Equal("33.3%", total.Cells[6]);
        Assert.Equal("15s", total.Cells[7]);
        Assert.Equal("1m 00s", total.Cells[8]);
    }

    [Fact]
    public void WorkforceUtilizationGrandTotal_RecomputesRatioFromAllIntervals()
    {
        // Arrange
        var startedUtc = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);
        var intervals = new[]
        {
            new AgentPresenceInterval
            {
                AgentId = "agent-1",
                Status = AgentPresenceStatus.Busy,
                StartUtc = startedUtc,
                EndUtc = startedUtc.AddHours(9),
            },
            new AgentPresenceInterval
            {
                AgentId = "agent-2",
                Status = AgentPresenceStatus.Available,
                StartUtc = startedUtc,
                EndUtc = startedUtc.AddHours(1),
            },
        };

        // Act
        var total = AgentWorkforceReportProvider.CreateUtilizationGrandTotalRow(
            intervals,
            "All agents",
            occupancy: false);

        // Assert
        Assert.Equal(ReportRowKind.GrandTotal, total.Kind);
        Assert.Equal("90.0%", total.Cells[3]);
    }

    [Fact]
    public void ProgressRows_PreserveSubtotalAndGrandTotalSemantics()
    {
        // Arrange
        var counts = new ActivityProgressCounts
        {
            Total = 3,
            Completed = 1,
        };

        // Act
        var subtotal = ContactCenterReportCells.ProgressRow("Group", counts, ReportRowKind.Subtotal);
        var grandTotal = ContactCenterReportCells.ProgressRow("All groups", counts, ReportRowKind.GrandTotal);

        // Assert
        Assert.Equal(ReportRowKind.Subtotal, subtotal.Kind);
        Assert.Equal(ReportRowKind.GrandTotal, grandTotal.Kind);
        Assert.Equal("33.3%", grandTotal.Cells[8]);
    }

    [Fact]
    public void QueueUsageGrandTotal_PreservesRawAggregateAndSemanticKind()
    {
        // Arrange
        var totals = new QueueUsageTotals
        {
            InteractionsHandled = 3,
            Answered = 2,
            Abandoned = 1,
            AverageHandleTimeSeconds = 45,
            AverageSpeedOfAnswerSeconds = 5,
            CurrentWaiting = 7,
        };

        // Act
        var row = QueueUsageReportProvider.CreateGrandTotalRow(
            totals,
            "All queues",
            includeSlaColumn: true);

        // Assert
        Assert.Equal(ReportRowKind.GrandTotal, row.Kind);
        Assert.Equal("45s", row.Cells[4]);
        Assert.Equal("7", row.Cells[6]);
        Assert.Equal("—", row.Cells[7]);
    }

    private static Interaction CreateInteraction(
        DateTime startedUtc,
        int? answeredAfterSeconds,
        int endedAfterSeconds)
    {
        return new Interaction
        {
            Direction = InteractionDirection.Inbound,
            Status = InteractionStatus.Ended,
            CreatedUtc = startedUtc,
            AnsweredUtc = answeredAfterSeconds.HasValue
                ? startedUtc.AddSeconds(answeredAfterSeconds.Value)
                : null,
            EndedUtc = startedUtc.AddSeconds(endedAfterSeconds),
        };
    }
}
