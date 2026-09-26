using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration.TransferIntegrationFixture;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A caller an agent blind-transfers has been talking to that agent, so nothing on the network is ringing for them:
/// sent to a queue with no music, or one that plays nothing at all, they heard silence until somebody picked up, which
/// sounds like the call dropped. They now hear the same waiting audio a caller from the phone menu does.
/// </summary>
public sealed class TransferWaitingAudioTests
{
    [Fact]
    public async Task BlindToAQueueWithNoMusic_WhileTheNextAgentRings_TheCallerHearsARingingToneUntilTheyAnswer()
    {
        // Arrange
        await using var fixture = await CreateAsync();
        fixture.Queues[SupportQueueId].Treatment = new QueueTreatmentSettings();
        var interaction = await fixture.FindInteractionAsync();

        // Act
        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();
        var heardWhileRinging = fixture.Treatment.RingbackStarted.ToArray();

        var offer = (await fixture.Reservations.GetActiveByAgentAsync(AgentB, TestContext.Current.CancellationToken)).Single();
        var accepted = await fixture.CallCommands.AcceptInboundOfferAsync(offer.ItemId, UserB, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        Assert.True(accepted.Succeeded, accepted.Reason);
        Assert.Equal([fixture.CallId], heardWhileRinging);
        Assert.DoesNotContain(fixture.Treatment.HoldMusicStarted, played => played.CallId == fixture.CallId);
        Assert.Contains(fixture.CallId, fixture.Treatment.HoldMusicStopped);
    }

    [Fact]
    public async Task BlindToAQueueThatPlaysNothing_WithNobodyFree_TheCallerHearsARingingTone()
    {
        // Arrange
        await using var fixture = await CreateAsync();
        fixture.Queues[SupportQueueId].Treatment = null;
        await fixture.Harness.PresenceManager.SetPresenceAsync(UserB, AgentPresenceStatus.Break, "Lunch", TestContext.Current.CancellationToken);
        var interaction = await fixture.FindInteractionAsync();

        // Act
        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(QueueItemStatus.Waiting, (await fixture.QueueItems.FindByActivityIdAsync(ActivityId, TestContext.Current.CancellationToken)).Status);
        Assert.Equal([fixture.CallId], fixture.Treatment.RingbackStarted);
    }

    [Fact]
    public async Task BlindToAQueueWithMusic_WithNobodyFree_TheCallerHearsTheMusicNotARingingTone()
    {
        // Arrange
        await using var fixture = await CreateAsync();
        await fixture.Harness.PresenceManager.SetPresenceAsync(UserB, AgentPresenceStatus.Break, "Lunch", TestContext.Current.CancellationToken);
        var interaction = await fixture.FindInteractionAsync();

        // Act
        await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Contains((fixture.CallId, "https://media.example.test/support.mp3"), fixture.Treatment.HoldMusicStarted);
        Assert.Empty(fixture.Treatment.RingbackStarted);
    }

    [Fact]
    public async Task BlindToAnAgentFromADirectCall_TheCallerHearsARingingToneWhileTheAgentRings()
    {
        // Arrange
        // A direct call belongs to no queue, so there was no music to play and nothing at all was played.
        await using var fixture = await CreateAsync();
        var interaction = await fixture.UseDirectCallAsync();

        // Act
        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Agent, AgentB, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal([fixture.CallId], fixture.Treatment.RingbackStarted);
    }

    [Fact]
    public async Task BlindToAQueueWithNoMusic_OnTelnyx_PlaysTheRingingToneToTheParkedCaller_BeforeTheAgentsLegIsHungUp()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":{\"id\":\"conf-1\"}}"),
        });

        await using var fixture = await CreateAsync(
            TelnyxContactCenterProviderFactory.Create(handler),
            TelnyxContactCenterProviderFactory.CreateTreatment(handler));
        fixture.Queues[SupportQueueId].Treatment = new QueueTreatmentSettings();
        var interaction = await fixture.FindInteractionAsync();

        // Act
        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.True(result.Succeeded, result.Reason);

        var commands = handler.Requests
            .Select((request, index) => (request.Method, Path: request.RequestUri.AbsolutePath, Body: handler.RequestBodies[index]))
            .Where(request => request.Method == HttpMethod.Post)
            .ToArray();

        // Parked on their own leg — into a conference of their own and straight out of it — then played the tone on
        // that leg, and only then is the agent's leg hung up.
        Assert.Equal(4, commands.Length);
        Assert.EndsWith("/v2/conferences", commands[0].Path, StringComparison.Ordinal);
        Assert.EndsWith("/v2/conferences/conf-1/actions/leave", commands[1].Path, StringComparison.Ordinal);
        Assert.EndsWith($"/v2/calls/{Uri.EscapeDataString(fixture.CallId)}/actions/playback_start", commands[2].Path, StringComparison.Ordinal);
        Assert.EndsWith($"/v2/calls/{AgentLegA}/actions/hangup", commands[3].Path, StringComparison.Ordinal);

        using var playback = JsonDocument.Parse(commands[2].Body);
        Assert.Equal("infinity", playback.RootElement.GetProperty("loop").GetString());
        Assert.False(string.IsNullOrEmpty(playback.RootElement.GetProperty("playback_content").GetString()));
    }
}
