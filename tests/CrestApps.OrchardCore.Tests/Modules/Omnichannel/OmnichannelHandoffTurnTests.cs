using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// The handoff decision used to live in process-wide <c>AsyncLocal</c> state. It is now a scoped service, so the
/// tool and the handler that ran the completion share one instance and two conversations processed at the same
/// time cannot see each other's decision.
/// </summary>
public sealed class OmnichannelHandoffTurnTests
{
    [Fact]
    public void ANewTurn_HasNoDecisionRecorded()
    {
        var turn = new OmnichannelHandoffTurn();

        Assert.False(turn.HandoffRequested);
        Assert.Null(turn.Reason);
    }

    [Fact]
    public void RequestHandoff_RecordsTheDecisionAndItsReason()
    {
        var turn = new OmnichannelHandoffTurn();

        turn.RequestHandoff("the customer asked for a person");

        Assert.True(turn.HandoffRequested);
        Assert.Equal("the customer asked for a person", turn.Reason);
    }

    [Fact]
    public void Reset_ClearsTheDecision_SoTheNextTurnStartsClean()
    {
        // The handler resets before each completion. Without it, one escalation would make every later turn on
        // the same scope look like an escalation too.
        var turn = new OmnichannelHandoffTurn();

        turn.RequestHandoff("frustrated");
        turn.Reset();

        Assert.False(turn.HandoffRequested);
        Assert.Null(turn.Reason);
    }

    [Fact]
    public void TwoTurns_DoNotSeeEachOthersDecision()
    {
        // The failure the scoped service removes: with ambient state, two conversations completing concurrently
        // in one process could read each other's escalation.
        var first = new OmnichannelHandoffTurn();
        var second = new OmnichannelHandoffTurn();

        first.RequestHandoff("escalate this one");

        Assert.True(first.HandoffRequested);
        Assert.False(second.HandoffRequested);
    }
}
