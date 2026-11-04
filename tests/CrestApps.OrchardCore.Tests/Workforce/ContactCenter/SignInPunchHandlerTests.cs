using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// Contact center sign-in punches: an agent who signs in is clocked in, and clocked out when they sign out, once per
/// event however often it is delivered, and never on top of a clock-in from somewhere else.
/// </summary>
public sealed class SignInPunchHandlerTests
{
    private static readonly DateOnly _monday = TimeClockKit.Monday;

    [Fact]
    public async Task SigningIn_ClocksIn_AndSigningOut_ClocksOut_OncePerEvent()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Support");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        var signIn = StateChange("e1", ana.UserId, TimeClockKit.Local(_monday, 9), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn);
        kit.Now = signIn.OccurredUtc.AddSeconds(5);

        await HandleAsync(kit, signIn);
        await HandleAsync(kit, signIn);

        var status = await StatusAsync(kit, ana);
        Assert.Equal(TimeClockState.Working, status.State);
        Assert.Equal(TimePunchSource.ContactCenter, status.LastPunch.Source);
        Assert.Equal(signIn.OccurredUtc, status.SinceUtc);

        var signOut = StateChange("e2", ana.UserId, TimeClockKit.Local(_monday, 17), AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut);
        kit.Now = signOut.OccurredUtc.AddSeconds(5);

        await HandleAsync(kit, signOut);

        Assert.Equal(TimeClockState.Out, (await StatusAsync(kit, ana)).State);
        Assert.Equal(2, (await PunchesAsync(kit, ana)).Count);
    }

    [Fact]
    public async Task AWorkerAlreadyClockedIn_IsLeftAlone()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Support");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        kit.Now = TimeClockKit.Local(_monday, 8, 55);
        Assert.True((await kit.PunchAsync(ana, TimePunchKind.In)).Succeeded);

        kit.Now = TimeClockKit.Local(_monday, 9, 1);
        await HandleAsync(kit, StateChange("e1", ana.UserId, TimeClockKit.Local(_monday, 9), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn));

        kit.Now = TimeClockKit.Local(_monday, 12, 1);
        await HandleAsync(kit, StateChange("e2", ana.UserId, TimeClockKit.Local(_monday, 12), AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut));

        // Ana clocked in on the web, so signing out of the contact center doesn't clock her out.
        var status = await StatusAsync(kit, ana);
        Assert.Equal(TimeClockState.Working, status.State);
        Assert.Single(await PunchesAsync(kit, ana));
    }

    [Fact]
    public async Task OtherStateChanges_AndUnknownUsers_AreIgnored()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Support");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        kit.Now = TimeClockKit.Local(_monday, 10, 1);
        await HandleAsync(kit, StateChange("e1", ana.UserId, TimeClockKit.Local(_monday, 10), AgentPresenceStatus.Available, AgentPresenceStatus.Busy, AgentStateChangeSources.Connected));
        await HandleAsync(kit, StateChange("e2", "nobody", TimeClockKit.Local(_monday, 10), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn));

        Assert.Empty(await PunchesAsync(kit, ana));
    }

    private static Task HandleAsync(TimeClockKit kit, InteractionEvent interactionEvent)
        => kit.Harness.RunAsync(services => ActivatorUtilities.CreateInstance<SignInPunchHandler>(services, NullLogger<SignInPunchHandler>.Instance).HandleAsync(interactionEvent, TestContext.Current.CancellationToken));

    private static Task<TimeClockStatus> StatusAsync(TimeClockKit kit, WorkerProfile worker)
        => kit.Harness.RunAsync(services => services.GetRequiredService<ITimeClockService>().GetStatusAsync(worker), commit: false);

    private static Task<IReadOnlyList<TimePunch>> PunchesAsync(TimeClockKit kit, WorkerProfile worker)
        => kit.Harness.RunAsync(services => services.GetRequiredService<ITimeClockService>().ListAsync(worker.ItemId, TimeClockKit.Local(_monday, 0), TimeClockKit.Local(_monday.AddDays(1), 0)), commit: false);

    private static InteractionEvent StateChange(string id, string userId, DateTime at, AgentPresenceStatus previous, AgentPresenceStatus current, string source)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = id,
            EventType = ContactCenterConstants.Events.AgentStateChanged,
            AggregateType = nameof(AgentProfile),
            AggregateId = "agent-" + userId,
            OccurredUtc = at,
            RecordedUtc = at,
        };

        interactionEvent.SetData(new AgentStateChangedEventData
        {
            AgentId = "agent-" + userId,
            UserId = userId,
            PreviousState = previous,
            CurrentState = current,
            Source = source,
            ChangedUtc = at,
        });

        return interactionEvent;
    }
}
