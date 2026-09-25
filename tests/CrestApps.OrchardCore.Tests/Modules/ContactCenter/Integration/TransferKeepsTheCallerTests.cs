using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration.TransferIntegrationFixture;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A blind transfer to a queue or an agent takes the transferring agent off a call that carries on without them. The
/// caller must still be on the line afterwards: on a provider whose agent leg is bridged to the caller, hanging that
/// leg up ends the caller with it unless the caller has first been taken out of the bridge. And the call must not be
/// handed straight back to the agent who just sent it away.
/// </summary>
public sealed class TransferKeepsTheCallerTests
{
    [Fact]
    public async Task BlindToQueue_TakesTheCallerOutOfTheAgentsBridge_BeforeTheAgentsLegIsHungUp()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal([$"park:{fixture.CallId}", $"release:{AgentLegA}"], fixture.Provider.LegCommands);
        Assert.DoesNotContain(fixture.CallId, fixture.Provider.ReleasedAgentLegs);
    }

    [Fact]
    public async Task BlindToAgent_TakesTheCallerOutOfTheAgentsBridge_BeforeTheAgentsLegIsHungUp()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Agent, AgentB, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal([$"park:{fixture.CallId}", $"release:{AgentLegA}"], fixture.Provider.LegCommands);
    }

    [Fact]
    public async Task BlindToQueue_WhenTheCallerCannotBeTakenOutOfTheBridge_IsRefused_AndTheCallStaysWithTheAgent()
    {
        await using var fixture = await CreateAsync();
        fixture.Provider.ParkSucceeds = false;
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Hanging the agent's leg up now would take the caller with it, so nothing moves.
        Assert.False(result.Succeeded);
        Assert.Empty(fixture.Provider.ReleasedAgentLegs);
        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Empty((await fixture.FindInteractionAsync()).TransferHistory);
        Assert.Null((await fixture.FindSessionAsync()).Legs.Single(leg => leg.ProviderLegId == AgentLegA).EndedUtc);
        Assert.Equal(DialerModeIntegrationHarness.QueueId, (await fixture.QueueItems.FindByActivityIdAsync(ActivityId, TestContext.Current.CancellationToken)).QueueId);
    }

    [Fact]
    public async Task BlindToExternal_LeavesMovingTheCallerToTheProvidersTransfer()
    {
        await using var fixture = await CreateAsync();
        fixture.ExternalSettings.AllowUnlistedNumbers = true;
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.External, "+15557654321", interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // The provider's own transfer moves the caller to the number; parking them first would pull them back out.
        Assert.True(result.Succeeded, result.Reason);
        Assert.Empty(fixture.Provider.ParkedCallers);
        Assert.Equal([$"release:{AgentLegA}"], fixture.Provider.LegCommands);
    }

    [Fact]
    public async Task BlindToQueue_IsNotOfferedBackToTheAgentWhoTransferredIt_ButWaitsWithTheQueuesTreatment()
    {
        await using var fixture = await CreateAsync();

        // A direct call leaves no after-call work, so releasing the agent makes them Available the moment the call
        // leaves them -- in time for the very offer that follows. Nobody else is free.
        var interaction = await fixture.FindInteractionAsync();
        interaction.QueueId = ContactCenterConstants.DirectRouting.QueueId;
        await fixture.Harness.InteractionManager.UpdateAsync(interaction, cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Harness.PresenceManager.SetPresenceAsync(UserB, AgentPresenceStatus.Break, "Lunch", TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(AgentPresenceStatus.Available, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Empty(await fixture.Reservations.GetActiveByAgentAsync(AgentA, TestContext.Current.CancellationToken));

        var queueItem = await fixture.QueueItems.FindByActivityIdAsync(ActivityId, TestContext.Current.CancellationToken);
        Assert.Equal(SupportQueueId, queueItem.QueueId);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        Assert.Contains(AgentA, queueItem.ExcludedAgentIds);
        Assert.Contains(fixture.Treatment.HoldMusicStarted, played => played.CallId == fixture.CallId);
    }

    [Fact]
    public async Task BlindToQueue_WhenTheCallerHangsUpWhileTheNextAgentIsRinging_ThatAgentIsFreedToo()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(AgentPresenceStatus.Reserved, await fixture.Harness.GetPresenceAsync(AgentB));

        // The caller gives up before B answers. A answered this call once, but that was before the transfer: nobody
        // is on it now, and B's offer must not be left holding B.
        await fixture.CallerHangsUpAsync();

        Assert.Equal(AgentPresenceStatus.Available, await fixture.Harness.GetPresenceAsync(AgentB));
        Assert.Null((await fixture.Harness.AgentManager.FindByIdAsync(AgentB, TestContext.Current.CancellationToken)).ActiveReservationId);
        Assert.Empty(await fixture.Reservations.GetActiveByAgentAsync(AgentB, TestContext.Current.CancellationToken));

        var queueItem = await fixture.QueueItems.FindByActivityIdAsync(ActivityId, TestContext.Current.CancellationToken);
        Assert.NotEqual(QueueItemStatus.Reserved, queueItem.Status);
        Assert.NotEqual(QueueItemStatus.Waiting, queueItem.Status);
    }

    [Fact]
    public async Task BlindToQueue_OnTelnyx_ParksTheCallerThenHangsUpOnlyTheAgentsLeg()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":{\"id\":\"conf-1\"}}"),
        });

        await using var fixture = await CreateAsync(TelnyxContactCenterProviderFactory.Create(handler));
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(result.Succeeded, result.Reason);

        var commands = handler.Requests
            .Select((request, index) => (request.Method, Path: request.RequestUri.AbsolutePath, Body: handler.RequestBodies[index]))
            .Where(request => request.Method == HttpMethod.Post)
            .ToArray();

        // The caller is moved into a conference of their own, which takes them out of the agent's bridge and parks the
        // agent's leg, and then leaves it, which parks the caller. Only then is the agent's leg hung up.
        Assert.Equal(3, commands.Length);
        Assert.EndsWith("/v2/conferences", commands[0].Path, StringComparison.Ordinal);
        Assert.EndsWith("/v2/conferences/conf-1/actions/leave", commands[1].Path, StringComparison.Ordinal);
        Assert.EndsWith($"/v2/calls/{AgentLegA}/actions/hangup", commands[2].Path, StringComparison.Ordinal);

        using (var create = JsonDocument.Parse(commands[0].Body))
        {
            Assert.Equal(fixture.CallId, create.RootElement.GetProperty("call_control_id").GetString());
            Assert.StartsWith("cc-park-", create.RootElement.GetProperty("name").GetString(), StringComparison.Ordinal);
        }

        using (var leave = JsonDocument.Parse(commands[1].Body))
        {
            Assert.Equal(fixture.CallId, leave.RootElement.GetProperty("call_control_id").GetString());
        }

        // Nothing ever hangs up the caller.
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri.AbsolutePath.EndsWith($"/calls/{Uri.EscapeDataString(fixture.CallId)}/actions/hangup", StringComparison.Ordinal));
    }
}
