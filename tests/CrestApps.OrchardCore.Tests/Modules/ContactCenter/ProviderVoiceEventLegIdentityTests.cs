using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.StateMachine;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Guards the conference a two-party call never was. The caller's leg reports its events under the provider's leg
/// identifier, while a per-call report (a reconciliation poll reading the call back from the provider) names only the
/// call. The per-call report was taken for a second caller, so a held two-party call counted three parties, became a
/// conference, and recorded a conference change with nothing about the call having changed; the hangup then recorded
/// a second one as the phantom party left.
/// </summary>
public sealed class ProviderVoiceEventLegIdentityTests
{
    private const string ProviderName = "Telnyx";
    private const string CallId = "v3:caller-call";
    private const string CallerLegId = "caller-leg";
    private const string AgentLegId = "v3:agent-leg";

    private static readonly DateTime _answeredUtc = new(2026, 9, 24, 19, 42, 28, DateTimeKind.Utc);

    [Fact]
    public async Task IngestAsync_WhenAPerCallReportArrivesForACallWhoseCallerIsKnownByItsLeg_KeepsItATwoPartyCall()
    {
        // Arrange
        var harness = await CreateHeldTwoPartyCallAsync();

        // Act: the reconciliation poll reads the held call back from the provider; it names the call, not a leg.
        await harness.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = ProviderName,
            ProviderCallId = CallId,
            State = VoiceCallState.OnHold,
            OccurredUtc = _answeredUtc.AddSeconds(72),
            IdempotencyKey = "reconcile-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(harness.Session.IsConference);
        Assert.Equal(2, harness.Session.Bridge.ActiveParticipants.Count());
        Assert.Single(harness.Session.Legs, leg => leg.Role == CallPartyRole.Customer);
        Assert.Equal(0, harness.CountPublished(ContactCenterConstants.Events.CallConferenceChanged));
    }

    [Fact]
    public async Task IngestAsync_WhenTheHeldCallThenEnds_RecordsNoConferenceChange()
    {
        // Arrange
        var harness = await CreateHeldTwoPartyCallAsync();

        await harness.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = ProviderName,
            ProviderCallId = CallId,
            State = VoiceCallState.OnHold,
            OccurredUtc = _answeredUtc.AddSeconds(72),
            IdempotencyKey = "reconcile-1",
        }, TestContext.Current.CancellationToken);

        // Act
        await harness.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = ProviderName,
            ProviderCallId = CallId,
            ProviderLegId = CallerLegId,
            State = VoiceCallState.Ended,
            HangupCause = HangupCause.NormalClearing,
            OccurredUtc = _answeredUtc.AddSeconds(84),
            IdempotencyKey = "hangup-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, harness.CountPublished(ContactCenterConstants.Events.CallEnded));
        Assert.Equal(0, harness.CountPublished(ContactCenterConstants.Events.CallConferenceChanged));

        // The caller's own leg is the one that ended with the hangup's cause, not a stand-in for it.
        var callerLeg = Assert.Single(harness.Session.Legs, leg => leg.Role == CallPartyRole.Customer);
        Assert.Equal(CallerLegId, callerLeg.ProviderLegId);
        Assert.Equal(HangupCause.NormalClearing, callerLeg.HangupCause);
    }

    private static async Task<CallStateMachineHarness> CreateHeldTwoPartyCallAsync()
    {
        var harness = new CallStateMachineHarness(ProviderName, CallId, "agent-1", _answeredUtc.AddMinutes(5));

        await harness.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = ProviderName,
            ProviderCallId = CallId,
            ProviderLegId = CallerLegId,
            State = VoiceCallState.Connected,
            OccurredUtc = _answeredUtc,
            IdempotencyKey = "bridged-1",
        }, TestContext.Current.CancellationToken);

        // The agent's leg joins the caller on the same bridge, as a pre-dialed or accept-time leg does.
        CallTopologyProjector.UpsertLeg(harness.Session, AgentLegId, CallPartyRole.Agent, CallLegStatus.Answered, _answeredUtc);
        CallTopologyProjector.Join(harness.Session, AgentLegId, CallPartyRole.Agent, _answeredUtc, "agent-1");

        await harness.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = ProviderName,
            ProviderCallId = CallId,
            ProviderLegId = CallerLegId,
            State = VoiceCallState.OnHold,
            OccurredUtc = _answeredUtc.AddSeconds(35),
            IdempotencyKey = "held-1",
        }, TestContext.Current.CancellationToken);

        Assert.False(harness.Session.IsConference);
        Assert.Equal(2, harness.Session.Bridge.ActiveParticipants.Count());

        return harness;
    }
}
