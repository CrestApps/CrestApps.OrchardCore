using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Leaving a conference made from a number dialed on the keypad and an extension call, followed through a model of
/// Telnyx's conferences to who is still on the line.
/// </summary>
/// <remarks>
/// <para>
/// Live: the agent dialed a cell, called extension 2 and merged them. The merge made a conference from the cell, joined
/// the agent's keypad leg, and joined the colleague into it -- moving them out of the extension call's own conference,
/// <c>ext-{agent's extension leg}</c>, which the colleague had created and the agent's extension leg had joined with
/// <c>end_conference_on_exit</c>. The agent pressed Leave: the agent's extension leg was hung up, the extension's own
/// conference ended, and Telnyx hung the colleague up (cause <c>time_limit</c>) although they had moved. The cell was
/// left alone in the merge's conference until they hung up.
/// </para>
/// <para>
/// A call that has created a conference is still bound to it after joining another one. It is freed only by leaving it
/// (<c>POST /conferences/{id}/actions/leave</c>, which "removes a call leg from a conference and moves it back to parked
/// state"): a caller that left the conference it was created from outlived that conference ending. So the colleague now
/// leaves the extension's own conference before joining the merge's.
/// </para>
/// </remarks>
public sealed class TelnyxLeaveMergedExtensionCallTests
{
    private const string Cell = "cell-leg";
    private const string KeypadAgentLeg = "keypad-agent";
    private const string ExtensionAgentLeg = "ext-agent";
    private const string Colleague = "colleague-leg";

    [Fact]
    public async Task LeavingAConferenceOfADialedNumberAndAnExtensionCall_KeepsTheCellAndTheColleagueConnected()
    {
        // Arrange
        var (network, provider, orchestrator) = ExtensionAndDialedNumber();
        await MergeAsync(network, provider, orchestrator);

        // Act - Leave: the phone hangs up each of the agent's calls flagged as leaving, the extension call first.
        await LeaveAsync(network, provider, orchestrator, ExtensionAgentLeg);
        await LeaveAsync(network, provider, orchestrator, KeypadAgentLeg);

        // Assert
        Assert.True(network.IsAlive(Cell), "The cell was hung up.");
        Assert.True(network.IsAlive(Colleague), "The colleague was hung up.");
        Assert.False(network.IsAlive(ExtensionAgentLeg));
        Assert.False(network.IsAlive(KeypadAgentLeg));
        Assert.Equal([Cell, Colleague], network.MembersOf($"conf-{KeypadAgentLeg}"));
    }

    [Fact]
    public async Task TheMerge_TakesTheColleagueOutOfTheExtensionsOwnConference_BeforeJoiningThemToTheMerges()
    {
        // Arrange
        var (network, provider, orchestrator) = ExtensionAndDialedNumber();

        // Act
        var result = await MergeAsync(network, provider, orchestrator);

        // Assert - the colleague leaves the conference they created, then joins; nothing else moves them.
        Assert.True(result.Succeeded, result.Error);
        var leave = network.Commands.IndexOf($"POST conferences/conference-1/actions/leave");
        var join = network.Commands.FindLastIndex(command => command == "POST conferences/conference-2/actions/join");
        Assert.True(leave >= 0, string.Join(Environment.NewLine, network.Commands));
        Assert.True(leave < join, string.Join(Environment.NewLine, network.Commands));
        Assert.Equal([Cell, Colleague, KeypadAgentLeg], network.MembersOf($"conf-{KeypadAgentLeg}"));
        Assert.Equal([ExtensionAgentLeg], network.MembersOf($"ext-{ExtensionAgentLeg}"));
    }

    // With no dialed number, the colleague leads: they leave the extension's own conference before a new one is made
    // from them, so the agent's extension leg leaving that old conference -- and ending it -- cannot reach them.
    [Fact]
    public async Task LeavingAConferenceLedByAnExtensionCall_KeepsTheOthersConnected()
    {
        // Arrange - a second extension call stands in for the other party.
        var network = new FakeTelnyxConferenceNetwork();
        ExtensionCall(network, ExtensionAgentLeg, Colleague);
        ExtensionCall(network, "ext-agent-2", "colleague-2");
        var provider = CreateProvider(network);
        var orchestrator = CreateOrchestrator(network);

        // Act
        var merged = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, "ext-agent-2"] }, TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);
        var conferenceName = merged.Call.Metadata["conferenceName"].ToString();
        await LeaveAsync(network, provider, orchestrator, ExtensionAgentLeg, conferenceName);
        await LeaveAsync(network, provider, orchestrator, "ext-agent-2", conferenceName);

        // Assert
        Assert.True(merged.Succeeded, merged.Error);
        Assert.True(network.IsAlive(Colleague), "The first colleague was hung up.");
        Assert.True(network.IsAlive("colleague-2"), "The second colleague was hung up.");
        Assert.Equal(["colleague-2", Colleague], network.MembersOf(conferenceName));
    }

    // Leaving must never strand one party alone in a conference: when only one would be left, it is ended.
    [Fact]
    public async Task LeavingWhenOnlyOnePartyIsStillInTheConference_EndsIt()
    {
        // Arrange - the colleague hangs up on their own after the merge, and the phone has not caught up.
        var (network, provider, orchestrator) = ExtensionAndDialedNumber();
        await MergeAsync(network, provider, orchestrator);
        network.PartyHangsUp(Colleague);
        await PumpAsync(network, orchestrator);
        Assert.True(network.IsAlive(Cell));

        // Act
        await LeaveAsync(network, provider, orchestrator, KeypadAgentLeg);

        // Assert
        Assert.False(network.IsAlive(Cell), "The cell was left alone in the conference.");
        Assert.Contains("POST conferences/conference-2/actions/end", network.Commands);
        Assert.Empty(network.MembersOf($"conf-{KeypadAgentLeg}"));
    }

    // The agent's keypad leg is still in the conference while the phone's leave for the extension call is handled: it is
    // not a party, so the colleague alone is one, and the conference ends then rather than after the second leave.
    [Fact]
    public async Task WhenTheCellHasHungUp_TheFirstLeaveEndsTheConference_WithTheAgentsOtherLegStillInIt()
    {
        // Arrange
        var (network, provider, orchestrator) = ExtensionAndDialedNumber();
        await MergeAsync(network, provider, orchestrator);
        network.PartyHangsUp(Cell);
        await PumpAsync(network, orchestrator);
        Assert.True(network.IsAlive(Colleague));

        // Act
        await LeaveAsync(network, provider, orchestrator, ExtensionAgentLeg);

        // Assert
        Assert.False(network.IsAlive(Colleague), "The colleague was left alone in the conference.");
        Assert.Contains("POST conferences/conference-2/actions/end", network.Commands);
    }

    [Fact]
    public async Task LeavingWhileTwoPartiesAreStillInTheConference_DoesNotEndIt()
    {
        // Arrange
        var (network, provider, orchestrator) = ExtensionAndDialedNumber();
        await MergeAsync(network, provider, orchestrator);

        // Act
        await LeaveAsync(network, provider, orchestrator, KeypadAgentLeg);

        // Assert - the agent's other leg is not a party: the cell and the colleague are two.
        Assert.DoesNotContain(network.Commands, command => command.EndsWith("/actions/end", StringComparison.Ordinal));
        Assert.True(network.IsAlive(Cell));
        Assert.True(network.IsAlive(Colleague));
    }

    // The live topology: a cell dialed from the keypad, bridged to the agent's keypad leg, and an extension call, whose
    // own conference was created from the colleague's leg and joined by the agent's extension leg with
    // end_conference_on_exit.
    private static (FakeTelnyxConferenceNetwork Network, TelnyxTelephonyProvider Provider, TelnyxOutboundBridgeOrchestrator Orchestrator) ExtensionAndDialedNumber()
    {
        var network = new FakeTelnyxConferenceNetwork();
        network.AddLeg(KeypadAgentLeg, TelnyxBridgedDialTests.AgentLeg(peer: Cell));
        network.AddLeg(Cell, new TelnyxOutboundBridgeState { Intent = TelnyxOutboundBridgeState.DestinationLegIntent, PeerCallControlId = KeypadAgentLeg });
        ExtensionCall(network, ExtensionAgentLeg, Colleague);

        return (network, CreateProvider(network), CreateOrchestrator(network));
    }

    private static void ExtensionCall(FakeTelnyxConferenceNetwork network, string agentLeg, string colleague)
    {
        network.AddLeg(agentLeg, TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: colleague));
        network.AddLeg(colleague, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = agentLeg,
            VoicemailRecipientUserId = "user-2",
        });

        var conference = network.CreateConference(TelnyxTelephonyProvider.ExtensionConferenceName(agentLeg), colleague);
        network.JoinConference(conference, agentLeg, endConferenceOnExit: true);
    }

    private static async Task<TelephonyResult> MergeAsync(
        FakeTelnyxConferenceNetwork network,
        TelnyxTelephonyProvider provider,
        TelnyxOutboundBridgeOrchestrator orchestrator)
    {
        // As the phone named them live: the extension call first.
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);

        return result;
    }

    private static async Task LeaveAsync(
        FakeTelnyxConferenceNetwork network,
        TelnyxTelephonyProvider provider,
        TelnyxOutboundBridgeOrchestrator orchestrator,
        string callId,
        string conferenceName = $"conf-{KeypadAgentLeg}")
    {
        var result = await provider.HangupAsync(new CallReference
        {
            CallId = callId,
            Metadata = new Dictionary<string, object>
            {
                [TelephonyConstants.RequestMetadata.ConferenceLeave] = true,
                ["isConference"] = true,
                ["conferenceName"] = conferenceName,
            },
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        await PumpAsync(network, orchestrator);
    }

    // Hands every hang-up to the orchestrator, as Telnyx's webhooks would, until nothing more ends.
    private static async Task PumpAsync(FakeTelnyxConferenceNetwork network, TelnyxOutboundBridgeOrchestrator orchestrator)
    {
        while (network.PendingEvents.TryDequeue(out var callEvent))
        {
            await orchestrator.AdvanceAsync(callEvent, TestContext.Current.CancellationToken);
        }
    }

    private static TelnyxOptions Options()
        => new()
        {
            IsEnabled = true,
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = "test-connection",
            DefaultOutboundCallerId = "+17785550000",
            OutboundVoiceProfileId = "voice-profile-1",
        };

    private static TelnyxApiClient ApiClient(HttpMessageHandler handler)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.com/v2/") },
            new OptionsWrapper<TelnyxOptions>(Options()),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

    private static TelnyxTelephonyProvider CreateProvider(HttpMessageHandler handler)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new TelnyxTelephonyProvider(
            ApiClient(handler),
            new Mock<ITelnyxAgentCredentialStore>().Object,
            new Mock<ITelnyxAgentEndpointResolver>().Object,
            clock.Object,
            NullLogger<TelnyxTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<TelnyxTelephonyProvider>(),
            new TestOptionsMonitor<TelnyxOptions>(Options()));
    }

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(HttpMessageHandler handler)
        => new(
            ApiClient(handler),
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            new TestOptionsMonitor<TelnyxOptions>(Options()),
            new Mock<IContactCenterAgentLegFailureService>().Object,
            [],
            [],
            []);
}
