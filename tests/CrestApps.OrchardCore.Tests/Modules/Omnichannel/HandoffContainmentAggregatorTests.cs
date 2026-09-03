using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public class HandoffContainmentAggregatorTests
{
    [Fact]
    public void Compute_CountsContainedVersusEscalated_AndRates()
    {
        var created = DateTime.UtcNow.AddMinutes(-30);

        var activities = new[]
        {
            Activity(escalated: false, null, created, created.AddMinutes(5)),   // contained
            Activity(escalated: false, null, created, created.AddMinutes(6)),   // contained
            Activity(escalated: true, OmnichannelConstants.TerminalReasons.HandedOffToAgent, created, created.AddMinutes(4)),          // SMS escalation
            Activity(escalated: true, OmnichannelConstants.TerminalReasons.HandedOffAfterHoursCallback, created, created.AddMinutes(8)), // after-hours
            // A routed voice escalation: its terminal reason is the call outcome, not a handoff code.
            Activity(escalated: true, "call_completed", created, created.AddMinutes(6)),
        };

        var summary = HandoffContainmentAggregator.Compute(activities);

        Assert.Equal(5, summary.Total);
        Assert.Equal(3, summary.Escalated);
        Assert.Equal(2, summary.Contained);
        Assert.Equal(0.4, summary.ContainmentRate);
        Assert.Equal(0.6, summary.EscalationRate);
        Assert.Equal(1, summary.EscalatedByReason[OmnichannelConstants.TerminalReasons.HandedOffToAgent]);
        Assert.Equal(1, summary.EscalatedByReason[OmnichannelConstants.TerminalReasons.HandedOffAfterHoursCallback]);
        // The routed voice escalation is bucketed under the routed-voice reason.
        Assert.Equal(1, summary.EscalatedByReason[HandoffContainmentAggregator.RoutedVoiceReason]);
        // Average of 4, 8, 6 minutes = 6 minutes.
        Assert.Equal(6, summary.AverageTimeToHandoff!.Value.TotalMinutes, precision: 3);
    }

    [Fact]
    public void Compute_WithNoActivities_ReturnsZeroedSummary()
    {
        var summary = HandoffContainmentAggregator.Compute([]);

        Assert.Equal(0, summary.Total);
        Assert.Equal(0, summary.ContainmentRate);
        Assert.Null(summary.AverageTimeToHandoff);
    }

    private static OmnichannelActivity Activity(bool escalated, string terminalReason, DateTime createdUtc, DateTime completedUtc)
        => new()
        {
            Source = ActivitySources.Automatic,
            Status = ActivityStatus.Completed,
            AiEscalated = escalated,
            TerminalReasonCode = terminalReason,
            CreatedUtc = createdUtc,
            CompletedUtc = completedUtc,
        };
}
