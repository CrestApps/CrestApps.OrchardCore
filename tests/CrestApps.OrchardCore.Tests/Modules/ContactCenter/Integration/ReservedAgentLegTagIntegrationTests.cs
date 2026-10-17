using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// The agent leg of a call placed for an agent already reserved for it (Power, Progressive, Preview) carries the
/// reservation the agent accepted, so the agent's phone answers it as that call's leg however long the customer took to
/// pick up, and a repeated answer of the customer never rings the agent a second leg.
/// </summary>
public sealed class ReservedAgentLegTagIntegrationTests
{
    private const string AgentOne = "agent-1";

    [Fact]
    public async Task Power_AnsweredCall_TagsTheAgentLegWithTheAcceptedReservation()
    {
        // Arrange
        await using var harness = await CreatePlacedPowerCallAsync();
        var reservation = Assert.Single(await harness.GetReservationsAsync());

        // The reservation the agent's phone heard accepted is kept on the call it was placed for.
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(reservation.ItemId, DialerCallMetadata.GetReservationId(interaction));

        // Act: the customer answers, long after the phone's short arm for the accept has lapsed.
        harness.Clock.Advance(TimeSpan.FromSeconds(45));
        await harness.RaiseCallStateAsync("activity-1", VoiceCallState.Connected, "connected");

        // Assert: the agent's leg is tagged with that reservation.
        var answer = Assert.Single(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Contains($"\"StandbyReservationId\":\"{reservation.ItemId}\"", answer.RequestPayload, StringComparison.Ordinal);
        Assert.Contains("\"AgentUserId\":\"user-1\"", answer.RequestPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Power_ConnectedDeliveredAgain_DoesNotRingTheAgentASecondLeg()
    {
        // Arrange
        await using var harness = await CreatePlacedPowerCallAsync();
        await harness.RaiseCallStateAsync("activity-1", VoiceCallState.Connected, "connected");
        var first = Assert.Single(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);

        // Act: the provider reports the call connected again (a bridge, a late answer-detection verdict).
        await harness.RaiseCallStateAsync("activity-1", VoiceCallState.Connected, "bridged");

        // Assert: still one connect, under the same command.
        var answer = Assert.Single(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Equal(first.CommandId, answer.CommandId);
    }

    private static async Task<DialerModeIntegrationHarness> CreatePlacedPowerCallAsync()
    {
        var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = DialerModeIntegrationHarness.CreateProfile(DialerMode.Power);
        Assert.Equal(1, await harness.RunPacingCycleAsync(profile));

        return harness;
    }
}
