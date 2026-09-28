using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A merged conference whose other parties have all hung up ends, rather than leaving the one still in it on a silent
/// line. Live: an agent merged a number and a colleague, left, and when the colleague hung up the number stayed connected
/// to nobody until they hung up themselves.
/// </summary>
public sealed class TelnyxMergedConferenceLastPartyTests
{
    private const string Number = "number-leg";
    private const string Colleague = "colleague-leg";
    private const string Agent = "agent-leg";

    [Fact]
    public async Task TheSecondToLastPartyHangingUp_EndsTheConference_ForTheOneLeft()
    {
        // Arrange
        var api = new FakeTelnyxCallControl();
        api.WithConference($"conf-{Agent}", Number, Colleague);
        HangUp(api, Colleague);
        var orchestrator = TelnyxSupervisorMonitoringTests.CreateOrchestrator(api, Mock.Of<ISupervisorLegEventSink>());

        // Act
        await orchestrator.AdvanceAsync(Left(Colleague), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("POST conferences/conf-1/actions/end", api.Commands);
        Assert.Empty(api.Conferences["conf-1"].Participants);
    }

    [Fact]
    public async Task APartyLeaving_WhileTwoOrMoreAreStillIn_EndsNothing()
    {
        // Arrange
        var api = new FakeTelnyxCallControl();
        api.WithConference($"conf-{Agent}", Number, Colleague, Agent);
        HangUp(api, Agent);
        var orchestrator = TelnyxSupervisorMonitoringTests.CreateOrchestrator(api, Mock.Of<ISupervisorLegEventSink>());

        // Act
        await orchestrator.AdvanceAsync(Left(Agent), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("POST conferences/conf-1/actions/end", api.Commands);
        Assert.Equal([Number, Colleague], api.Conferences["conf-1"].Participants);
    }

    // A leg that left without hanging up is being moved (a merge undone, a call joined elsewhere): the conference is being
    // rearranged, not abandoned.
    [Fact]
    public async Task ALegThatLeftButIsStillUp_EndsNothing()
    {
        // Arrange
        var api = new FakeTelnyxCallControl();
        api.WithConference($"conf-{Agent}", Number);
        var orchestrator = TelnyxSupervisorMonitoringTests.CreateOrchestrator(api, Mock.Of<ISupervisorLegEventSink>());

        // Act
        await orchestrator.AdvanceAsync(Left(Colleague), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("POST conferences/conf-1/actions/end", api.Commands);
        Assert.Equal([Number], api.Conferences["conf-1"].Participants);
    }

    // A supervised call's conference drops to one participant on its way back to its bridge, and an extension call's own
    // conference has rules of its own: only a merge's conference is ended here.
    [Theory]
    [InlineData("cc-sv-number-leg")]
    [InlineData("ext-agent-leg")]
    public async Task OnlyAMergesConference_IsEnded(string conferenceName)
    {
        // Arrange
        var api = new FakeTelnyxCallControl();
        api.WithConference(conferenceName, Number, Colleague);
        HangUp(api, Colleague);
        var orchestrator = TelnyxSupervisorMonitoringTests.CreateOrchestrator(api, Mock.Of<ISupervisorLegEventSink>());

        // Act
        await orchestrator.AdvanceAsync(Left(Colleague), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("POST conferences/conf-1/actions/end", api.Commands);
        Assert.Equal([Number], api.Conferences["conf-1"].Participants);
    }

    private static void HangUp(FakeTelnyxCallControl api, string leg)
    {
        api.HungUp.Add(leg);

        foreach (var conference in api.Conferences.Values)
        {
            conference.Participants.Remove(leg);
        }
    }

    private static TelnyxCallEvent Left(string leg)
        => new()
        {
            EventType = "conference.participant.left",
            CallControlId = leg,
            ConferenceId = "conf-1",
        };
}
