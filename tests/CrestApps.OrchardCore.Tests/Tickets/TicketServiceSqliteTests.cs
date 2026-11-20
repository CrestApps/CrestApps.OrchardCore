using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket service on a real store with the real tracker and timeline, one scope per unit of work. A change is
/// committed while the ticket is locked, so two agents replying at once both count; observers are told after the lock
/// is released, so a rule that changes the same ticket (as a workflow does) succeeds instead of waiting on itself, and a
/// rule on Ticket Created isn't overwritten; a refused reply leaves nothing behind; two opposite merges can't both win;
/// solving a ticket without an answer ends its response targets, and a ticket that waited and is reopened counts again;
/// an automated reply doesn't count as an agent's answer.
/// </summary>
public sealed class TicketServiceSqliteTests
{
    [Fact]
    public async Task ARefusedUpdate_StagesNothing_NotEvenARetargetedTimer()
    {
        // Arrange: a ticket on a policy, so a priority change retargets its timers.
        await using var kit = await TicketKit.CreateAsync(withPolicy: true);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        var before = (await kit.TimersAsync(ticketId)).Select(timer => (timer.Metric, timer.Priority, timer.DueUtc)).OrderBy(pair => pair.Metric).ToList();

        // Act: a valid priority with a status that doesn't exist; the request commits whatever is pending.
        var result = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, new TicketUpdate { PriorityId = TicketKit.Urgent.ItemId, StatusId = "no-such-status" }, cancellationToken: TestContext.Current.CancellationToken));
        var after = (await kit.TimersAsync(ticketId)).Select(timer => (timer.Metric, timer.Priority, timer.DueUtc)).OrderBy(pair => pair.Metric).ToList();
        var part = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(before, after);
        Assert.Equal(TicketKit.Normal.ItemId, part.PriorityId);
    }

    [Fact]
    public async Task TwoAgentsReplyingAtOnce_BothCount_AndTheStatusChangeIsKept()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var replies = await Task.WhenAll(
            kit.RunAsync(services => services.GetRequiredService<ITicketService>().ReplyAsync(ticketId, Reply("First", TicketKit.Pending.ItemId), TestContext.Current.CancellationToken)),
            kit.RunAsync(services => services.GetRequiredService<ITicketService>().ReplyAsync(ticketId, Reply("Second"), TestContext.Current.CancellationToken)));

        // Assert
        Assert.All(replies, reply => Assert.True(reply.Succeeded, string.Join(" ", reply.Errors)));
        var part = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        Assert.Equal(3, part.ReplyCount);
        Assert.Equal(2, part.AgentReplyCount);
        Assert.Equal(TicketKit.Pending.ItemId, part.StatusId);
    }

    [Fact]
    public async Task ARuleThatChangesTheTicket_Succeeds_BecauseObserversAreToldAfterTheLock()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        var results = new List<TicketResult>();
        kit.Observers.React = async (services, name, ticket) =>
        {
            var tickets = services.GetRequiredService<ITicketService>();

            if (name == "CustomerReplied")
            {
                results.Add(await tickets.ReplyAsync(ticket.ContentItemId, new TicketReply { Body = "Auto note", IsInternal = true, Actor = new TicketActor { FromWorkflow = true } }));
            }
            else if (name.StartsWith("StatusChanged:", StringComparison.Ordinal))
            {
                results.Add(await tickets.UpdateAsync(ticket.ContentItemId, new TicketUpdate { PriorityId = TicketKit.Urgent.ItemId }, new TicketActor { FromWorkflow = true }));
            }
        };

        // Act
        var started = DateTime.UtcNow;
        var reply = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().ReplyAsync(ticketId, new TicketReply
        {
            Body = "Any news?",
            FromRequester = true,
            Author = new PartyRef(PartyKinds.Customer, "c1"),
        }, TestContext.Current.CancellationToken));
        kit.Observers.React = null;

        // Assert
        Assert.True(reply.Succeeded);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5), "A rule waited on the ticket's lock.");
        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.True(result.Succeeded, string.Join(" ", result.Errors)));
        Assert.Contains(await kit.EntriesAsync(ticketId), entry => entry.Body == "Auto note");
    }

    [Fact]
    public async Task ARuleOnTicketCreated_IsNotOverwrittenByTheCreation()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        kit.Observers.React = (services, name, ticket) => name == "Created"
            ? services.GetRequiredService<ITicketService>().UpdateAsync(ticket.ContentItemId, new TicketUpdate { PriorityId = TicketKit.Urgent.ItemId }, new TicketActor { FromWorkflow = true })
            : Task.CompletedTask;

        // Act
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        kit.Observers.React = null;

        // Assert
        Assert.Equal(TicketKit.Urgent.ItemId, (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().PriorityId);
    }

    [Fact]
    public async Task AReplyWithAStatusItCantMoveTo_LeavesNothingBehind()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(withPolicy: true);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        var before = (await kit.EntriesAsync(ticketId)).Count;

        // Act
        var refused = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().ReplyAsync(ticketId, Reply("Done?", "missing-status"), TestContext.Current.CancellationToken));

        // Assert
        Assert.False(refused.Succeeded);
        Assert.Equal(before, (await kit.EntriesAsync(ticketId)).Count);
        var part = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        Assert.Null(part.FirstResponseUtc);
        Assert.Equal(1, part.ReplyCount);
        Assert.Contains(await kit.TimersAsync(ticketId), timer => timer.Metric == ServiceLevelMetrics.FirstResponse && timer.IsActive);
    }

    [Fact]
    public async Task TwoOppositeMerges_CantBothSucceed()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var a = (await kit.CreateAsync("A")).Ticket.ContentItemId;
        var b = (await kit.CreateAsync("B")).Ticket.ContentItemId;

        // Act
        var results = await Task.WhenAll(
            kit.RunAsync(services => services.GetRequiredService<ITicketService>().MergeAsync(a, b, cancellationToken: TestContext.Current.CancellationToken)),
            kit.RunAsync(services => services.GetRequiredService<ITicketService>().MergeAsync(b, a, cancellationToken: TestContext.Current.CancellationToken)));

        // Assert
        Assert.Single(results, result => result.Succeeded);
        var merged = new[] { await kit.GetAsync(a), await kit.GetAsync(b) }.Count(ticket => !string.IsNullOrEmpty(ticket.GetOrCreate<TicketPart>().MergedIntoId));
        Assert.Equal(1, merged);
    }

    [Fact]
    public async Task SolvingWithoutAnAnswer_EndsTheResponseTargets()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(withPolicy: true);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var solved = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, new TicketUpdate { StatusId = TicketKit.Solved.ItemId }, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.True(solved.Succeeded, string.Join(" ", solved.Errors));
        Assert.DoesNotContain(await kit.TimersAsync(ticketId), timer => timer.IsActive);
        Assert.Null((await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().NextDueUtc);
    }

    [Fact]
    public async Task ATicketThatWaitedAndIsReopened_CountsAgain()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(withPolicy: true);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        foreach (var status in new[] { TicketKit.Pending, TicketKit.Solved, TicketKit.Open })
        {
            var moved = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, new TicketUpdate { StatusId = status.ItemId }, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(moved.Succeeded, string.Join(" ", moved.Errors));
        }

        // Assert
        var active = (await kit.TimersAsync(ticketId)).Where(timer => timer.IsActive).ToList();
        Assert.Contains(active, timer => timer.Metric == ServiceLevelMetrics.Resolution);
        Assert.All(active, timer => Assert.Equal(ServiceLevelTimerState.Running, timer.State));
        Assert.NotNull((await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().NextDueUtc);
    }

    [Fact]
    public async Task AnAutomatedReply_DoesntCountAsAnAgentsAnswer()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(withPolicy: true);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        await kit.RunAsync(services => services.GetRequiredService<ITicketService>().ReplyAsync(ticketId, new TicketReply
        {
            Body = "We got your request.",
            Actor = new TicketActor { FromWorkflow = true, Name = "Workflow" },
        }, TestContext.Current.CancellationToken));

        // Assert
        var part = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        Assert.Null(part.FirstResponseUtc);
        Assert.Equal(0, part.AgentReplyCount);
        Assert.Contains(await kit.TimersAsync(ticketId), timer => timer.Metric == ServiceLevelMetrics.FirstResponse && timer.IsActive);
    }

    private static TicketReply Reply(string body, string statusId = null)
        => new()
        {
            Body = body,
            Author = new PartyRef(PartyKinds.User, "agent"),
            AuthorName = "Agent",
            SetStatusId = statusId,
        };
}
