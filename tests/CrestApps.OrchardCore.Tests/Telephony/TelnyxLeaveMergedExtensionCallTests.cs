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
/// Merging extension calls with other calls and leaving the conference, followed through a model of Telnyx's
/// conferences (<see cref="FakeTelnyxConferenceNetwork"/>) and the real orchestrator to who is still on the line.
/// </summary>
/// <remarks>
/// <para>
/// Live, twice: the agent dialed a cell, called extension 2, merged them and pressed Leave. The colleague was hung up
/// (cause <c>time_limit</c>) and the cell was left alone. The extension call's own conference had been made from the
/// colleague's leg, and the agent's extension leg had joined it with <c>end_conference_on_exit</c>. When the agent's leg
/// hung up, that conference was ended, and Telnyx hung up the call it was made from. It did so although the colleague
/// had moved to the merge's conference, the second time even after leaving the old one with <c>actions/leave</c> first.
/// </para>
/// <para>
/// The extension call's conference is now made from the agent's own leg, and nobody joins it with
/// <c>end_conference_on_exit</c>. The only call its end can take down is the agent's.
/// </para>
/// </remarks>
public sealed class TelnyxLeaveMergedExtensionCallTests
{
    private const string Cell = "cell-leg";
    private const string KeypadAgentLeg = "keypad-agent";
    private const string ExtensionAgentLeg = "ext-agent";
    private const string Colleague = "colleague-leg";
    private const string CallerLeg = "caller-leg";

    [Fact]
    public async Task AnExtensionCall_IsAConferenceMadeFromTheAgentsLeg_ThatNobodyEndsOnExit()
    {
        // Arrange
        var (network, _, orchestrator) = CreateNetwork();

        // Act
        await ExtensionCallAsync(network, orchestrator, ExtensionAgentLeg, Colleague);

        // Assert
        Assert.Contains($"POST conferences", network.Commands);
        Assert.Equal([Colleague, ExtensionAgentLeg], network.MembersOf($"ext-{ExtensionAgentLeg}"));
        Assert.True(network.StateOf(ExtensionAgentLeg).PeerAnswered);

        // The agent hanging up still ends it for the colleague, and the colleague hanging up for the agent.
        network.PartyHangsUp(ExtensionAgentLeg);
        await PumpAsync(network, orchestrator);
        Assert.False(network.IsAlive(Colleague));
    }

    [Fact]
    public async Task TheColleagueHangingUp_EndsTheAgentsExtensionLeg()
    {
        // Arrange
        var (network, _, orchestrator) = CreateNetwork();
        await ExtensionCallAsync(network, orchestrator, ExtensionAgentLeg, Colleague);

        // Act
        network.PartyHangsUp(Colleague);
        await PumpAsync(network, orchestrator);

        // Assert
        Assert.False(network.IsAlive(ExtensionAgentLeg));
    }

    // The live sequence: a cell dialed from the keypad, then extension 2, merged (the extension call named first), then
    // Leave -- the phone hangs up each of the agent's calls flagged as leaving, the extension call first.
    [Fact]
    public async Task LeavingAConferenceOfADialedNumberAndAnExtensionCall_KeepsTheCellAndTheColleagueConnected()
    {
        // Arrange
        var (network, provider, orchestrator) = await KeypadAndExtensionAsync();
        var merged = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);
        Assert.True(merged.Succeeded, merged.Error);

        // Act
        await LeaveAsync(network, provider, orchestrator, ExtensionAgentLeg);
        await LeaveAsync(network, provider, orchestrator, KeypadAgentLeg);

        // Assert
        Assert.True(network.IsAlive(Cell), "The cell was hung up.");
        Assert.True(network.IsAlive(Colleague), "The colleague was hung up.");
        Assert.False(network.IsAlive(ExtensionAgentLeg));
        Assert.False(network.IsAlive(KeypadAgentLeg));
        Assert.Equal([Cell, Colleague], network.MembersOf($"conf-{KeypadAgentLeg}"));
    }

    // Two colleagues: the first one's conference gives way to a new one made from them, and the agent's first extension
    // leg leaves its own conference before joining; the second colleague joins it.
    [Fact]
    public async Task LeavingAConferenceOfTwoExtensionCalls_KeepsBothColleaguesConnected()
    {
        // Arrange
        var (network, provider, orchestrator) = CreateNetwork();
        await ExtensionCallAsync(network, orchestrator, ExtensionAgentLeg, Colleague);
        await ExtensionCallAsync(network, orchestrator, "ext-agent-2", "colleague-2");

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

    [Fact]
    public async Task LeavingAConferenceOfACallerAndAnExtensionCall_KeepsTheCallerAndTheColleagueConnected()
    {
        // Arrange - a Contact Center caller's own leg, and an extension call.
        var (network, provider, orchestrator) = CreateNetwork();
        network.AddLeg(CallerLeg, state: null);
        await ExtensionCallAsync(network, orchestrator, ExtensionAgentLeg, Colleague);

        // Act - the phone keeps the Contact Center call when leaving, so only the extension call is left.
        var merged = await provider.MergeAsync(new MergeRequest { CallIds = [CallerLeg, ExtensionAgentLeg] }, TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);
        var conferenceName = merged.Call.Metadata["conferenceName"].ToString();
        await LeaveAsync(network, provider, orchestrator, ExtensionAgentLeg, conferenceName);

        // Assert
        Assert.True(merged.Succeeded, merged.Error);
        Assert.True(network.IsAlive(CallerLeg));
        Assert.True(network.IsAlive(Colleague));
        Assert.Equal([CallerLeg, Colleague], network.MembersOf(conferenceName));
    }

    // Live: Merge was pressed while extension 2 was still ringing. Telnyx refused the colleague's join ("Call not answered
    // yet") after the conference had been made and the cell and the agent moved into it, and every retry then failed on
    // the conference's name.
    [Fact]
    public async Task MergingBeforeTheExtensionAnswers_IsRefused_BeforeAnythingMoves()
    {
        // Arrange - the colleague is ringing.
        var (network, provider, orchestrator) = CreateNetwork();
        await KeypadCallAsync(network, orchestrator);
        network.AddLeg(ExtensionAgentLeg, Ringing(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: Colleague)));
        network.AddLeg(Colleague, ColleagueState(ExtensionAgentLeg), answered: false);

        // Act
        network.Commands.Clear();
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(TelephonyConstants.ErrorCodes.NotAnswered, result.ErrorCode);
        Assert.Contains("not been answered", result.Error, StringComparison.Ordinal);
        Assert.All(network.Commands, command => Assert.StartsWith("GET ", command, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MergingBeforeTheDialedNumberAnswers_IsRefused_BeforeAnythingMoves()
    {
        // Arrange - the cell is ringing.
        var (network, provider, orchestrator) = CreateNetwork();
        network.AddLeg(KeypadAgentLeg, Ringing(TelnyxBridgedDialTests.AgentLeg(peer: Cell)));
        network.AddLeg(Cell, CellState(), answered: false);
        await ExtensionCallAsync(network, orchestrator, ExtensionAgentLeg, Colleague);

        // Act
        network.Commands.Clear();
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelephonyConstants.ErrorCodes.NotAnswered, result.ErrorCode);
        Assert.DoesNotContain(network.Commands, command => command.StartsWith("POST ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AMergeWhoseJoinIsRefused_PutsTheCallsBackWhereTheyWere()
    {
        // Arrange - Telnyx refuses the colleague's join.
        var (network, provider, orchestrator) = await KeypadAndExtensionAsync();
        network.RefuseJoinsOf(Colleague);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);

        // Assert - nobody is left in the merge's conference, the cell is attached and bridged to the agent's keypad leg
        // again, and everyone is still on the line.
        Assert.False(result.Succeeded);
        Assert.Empty(network.MembersOf($"conf-{KeypadAgentLeg}"));
        Assert.Equal("POST calls/keypad-agent/actions/bridge", network.Commands[^1]);
        Assert.NotEqual(true, network.StateOf(Cell).Detached);
        Assert.Equal(KeypadAgentLeg, network.StateOf(Cell).PeerCallControlId);
        Assert.True(network.IsAlive(Cell));
        Assert.True(network.IsAlive(KeypadAgentLeg));
        Assert.True(network.IsAlive(Colleague));
        Assert.Equal([Colleague, ExtensionAgentLeg], network.MembersOf($"ext-{ExtensionAgentLeg}"));
    }

    // Live, every retry after the half-built merge failed with "Conference with given name already exists".
    [Fact]
    public async Task ARetry_UsesTheConferenceAnEarlierAttemptLeft()
    {
        // Arrange - a conference of the merge's name with the cell and the agent's keypad leg in it.
        var (network, provider, orchestrator) = await KeypadAndExtensionAsync();
        var leftover = network.CreateConference($"conf-{KeypadAgentLeg}", Cell);
        network.JoinConference(leftover, KeypadAgentLeg, endConferenceOnExit: false);

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal([Cell, Colleague, KeypadAgentLeg], network.MembersOf($"conf-{KeypadAgentLeg}"));
        Assert.True(network.StateOf(Cell).Detached);
    }

    // Leaving must never strand one party alone in a conference: when only one would be left, it is ended.
    [Fact]
    public async Task LeavingWhenOnlyOnePartyIsStillInTheConference_EndsIt()
    {
        // Arrange - the colleague hangs up on their own after the merge, and the phone has not caught up.
        var (network, provider, orchestrator) = await KeypadAndExtensionAsync();
        await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);
        network.PartyHangsUp(Colleague);
        await PumpAsync(network, orchestrator);
        Assert.True(network.IsAlive(Cell));

        // Act
        await LeaveAsync(network, provider, orchestrator, KeypadAgentLeg);

        // Assert
        Assert.False(network.IsAlive(Cell), "The cell was left alone in the conference.");
        Assert.Empty(network.MembersOf($"conf-{KeypadAgentLeg}"));
    }

    // The agent's keypad leg is still in the conference while the phone's leave for the extension call is handled: it is
    // not a party, so the colleague alone is one, and the conference ends then rather than after the second leave.
    [Fact]
    public async Task WhenTheCellHasHungUp_TheFirstLeaveEndsTheConference_WithTheAgentsOtherLegStillInIt()
    {
        // Arrange
        var (network, provider, orchestrator) = await KeypadAndExtensionAsync();
        await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);
        network.PartyHangsUp(Cell);
        await PumpAsync(network, orchestrator);
        Assert.True(network.IsAlive(Colleague));

        // Act
        await LeaveAsync(network, provider, orchestrator, ExtensionAgentLeg);

        // Assert
        Assert.False(network.IsAlive(Colleague), "The colleague was left alone in the conference.");
        Assert.Contains(network.Commands, command => command.EndsWith("/actions/end", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LeavingWhileTwoPartiesAreStillInTheConference_DoesNotEndIt()
    {
        // Arrange
        var (network, provider, orchestrator) = await KeypadAndExtensionAsync();
        await provider.MergeAsync(new MergeRequest { CallIds = [ExtensionAgentLeg, KeypadAgentLeg] }, TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);

        // Act
        await LeaveAsync(network, provider, orchestrator, KeypadAgentLeg);

        // Assert
        Assert.DoesNotContain(network.Commands, command => command.EndsWith("/actions/end", StringComparison.Ordinal));
        Assert.True(network.IsAlive(Cell));
        Assert.True(network.IsAlive(Colleague));
    }

    private static (FakeTelnyxConferenceNetwork Network, TelnyxTelephonyProvider Provider, TelnyxOutboundBridgeOrchestrator Orchestrator) CreateNetwork()
    {
        var network = new FakeTelnyxConferenceNetwork();

        return (network, CreateProvider(network), CreateOrchestrator(network));
    }

    private static async Task<(FakeTelnyxConferenceNetwork Network, TelnyxTelephonyProvider Provider, TelnyxOutboundBridgeOrchestrator Orchestrator)> KeypadAndExtensionAsync()
    {
        var (network, provider, orchestrator) = CreateNetwork();
        await KeypadCallAsync(network, orchestrator);
        await ExtensionCallAsync(network, orchestrator, ExtensionAgentLeg, Colleague);
        network.Commands.Clear();

        return (network, provider, orchestrator);
    }

    // A cell dialed from the keypad, as the orchestrator leaves it once the cell answers: bridged to the agent's leg, and
    // the agent's leg noting that it answered.
    private static async Task KeypadCallAsync(FakeTelnyxConferenceNetwork network, TelnyxOutboundBridgeOrchestrator orchestrator)
    {
        network.AddLeg(KeypadAgentLeg, Ringing(TelnyxBridgedDialTests.AgentLeg(peer: Cell)));
        network.AddLeg(Cell, CellState(), answered: false);
        await orchestrator.AdvanceAsync(network.Answer(Cell), TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);
    }

    // An extension call, connected by the orchestrator when the colleague answers.
    private static async Task ExtensionCallAsync(FakeTelnyxConferenceNetwork network, TelnyxOutboundBridgeOrchestrator orchestrator, string agentLeg, string colleague)
    {
        network.AddLeg(agentLeg, Ringing(TelnyxMergeExtensionCallTests.ExtensionAgentState(peer: colleague)));
        network.AddLeg(colleague, ColleagueState(agentLeg), answered: false);
        await orchestrator.AdvanceAsync(network.Answer(colleague), TestContext.Current.CancellationToken);
        await PumpAsync(network, orchestrator);
    }

    // As the orchestrator records the party's leg when it dials it: not answered yet.
    private static TelnyxOutboundBridgeState Ringing(TelnyxOutboundBridgeState state)
    {
        state.PeerAnswered = false;

        return state;
    }

    private static TelnyxOutboundBridgeState CellState()
        => new() { Intent = TelnyxOutboundBridgeState.DestinationLegIntent, PeerCallControlId = KeypadAgentLeg };

    private static TelnyxOutboundBridgeState ColleagueState(string agentLeg)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
            PeerCallControlId = agentLeg,
            VoicemailRecipientUserId = "user-2",
        };

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
