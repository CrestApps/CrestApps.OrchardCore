using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A ticket's planning fields: impact and urgency set the priority through the desk's priority matrix (when the matrix
/// names one), the estimate and the billing type are kept and can be cleared, every change is on the timeline, and the
/// person who solved the ticket is kept on it and in the index.
/// </summary>
public sealed class TicketPlanningTests
{
    [Fact]
    public async Task ImpactAndUrgency_SetThePriority_ThroughTheDesksMatrix()
    {
        // Arrange (high impact and high urgency is Urgent; nothing else is in the matrix)
        await using var kit = await TicketKit.CreateAsync(configureDesk: desk => desk.PriorityMatrix =
        [
            new TicketPriorityRule { Impact = TicketLevel.High, Urgency = TicketLevel.High, PriorityId = TicketKit.Urgent.ItemId },
        ]);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var lowUrgency = await UpdateAsync(kit, ticketId, new TicketUpdate { Impact = TicketLevel.High, Urgency = TicketLevel.Low });
        var afterLow = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        var highUrgency = await UpdateAsync(kit, ticketId, new TicketUpdate { Urgency = TicketLevel.High });
        var afterHigh = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        var entries = await kit.EntriesAsync(ticketId);

        // Assert
        Assert.True(lowUrgency.Succeeded && highUrgency.Succeeded);
        Assert.Equal(TicketLevel.High, afterLow.Impact);
        Assert.Equal(TicketKit.Normal.ItemId, afterLow.PriorityId);
        Assert.Equal(TicketLevel.High, afterHigh.Urgency);
        Assert.Equal(TicketKit.Urgent.ItemId, afterHigh.PriorityId);
        Assert.Equal(TicketKit.Urgent.Rank, afterHigh.PriorityRank);
        var keys = entries.SelectMany(entry => entry.Changes).Select(change => change.Key).ToList();
        Assert.Contains(TicketChangeKeys.Impact, keys);
        Assert.Contains(TicketChangeKeys.Urgency, keys);
        Assert.Contains(TicketChangeKeys.Priority, keys);
    }

    [Fact]
    public async Task ANewTicketsImpactAndUrgency_PickItsPriority_UnlessTheRequestNamesOne()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(configureDesk: desk => desk.PriorityMatrix =
        [
            new TicketPriorityRule { Impact = TicketLevel.High, Urgency = TicketLevel.Medium, PriorityId = TicketKit.Urgent.ItemId },
        ]);

        // Act
        var fromMatrix = await CreateAsync(kit, null);
        var named = await CreateAsync(kit, TicketKit.Normal.ItemId);

        // Assert
        Assert.Equal(TicketKit.Urgent.ItemId, fromMatrix.GetOrCreate<TicketPart>().PriorityId);
        Assert.Equal(TicketLevel.High, fromMatrix.GetOrCreate<TicketPart>().Impact);
        Assert.Equal(TicketKit.Normal.ItemId, named.GetOrCreate<TicketPart>().PriorityId);
    }

    [Fact]
    public async Task TheEstimateAndBillingType_AreKept_AndTheEstimateCanBeCleared()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        var start = new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);

        // Act
        var set = await UpdateAsync(kit, ticketId, new TicketUpdate { SetEstimate = true, EstimatedStartUtc = start, EstimatedMinutes = 90, BillingType = TicketBillingType.Agreement });
        var withEstimate = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        var tooLong = await UpdateAsync(kit, ticketId, new TicketUpdate { SetEstimate = true, EstimatedMinutes = 100_000 });
        var cleared = await UpdateAsync(kit, ticketId, new TicketUpdate { SetEstimate = true });
        var withoutEstimate = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        var keys = (await kit.EntriesAsync(ticketId)).SelectMany(entry => entry.Changes).Select(change => change.Key).ToList();

        // Assert
        Assert.True(set.Succeeded && cleared.Succeeded);
        Assert.False(tooLong.Succeeded);
        Assert.Equal(start, withEstimate.EstimatedStartUtc);
        Assert.Equal(90, withEstimate.EstimatedMinutes);
        Assert.Equal(TicketBillingType.Agreement, withEstimate.BillingType);
        Assert.Null(withoutEstimate.EstimatedStartUtc);
        Assert.Null(withoutEstimate.EstimatedMinutes);
        Assert.Equal(TicketBillingType.Agreement, withoutEstimate.BillingType);
        Assert.Contains(TicketChangeKeys.Estimate, keys);
        Assert.Contains(TicketChangeKeys.BillingType, keys);
    }

    [Fact]
    public async Task WhoSolvedTheTicket_IsKeptOnItAndInTheIndex()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var byUpdate = (await kit.CreateAsync()).Ticket.ContentItemId;
        var byReply = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        await UpdateAsync(kit, byUpdate, new TicketUpdate { StatusId = TicketKit.Solved.ItemId }, TicketActor.User("ana", "Ana"));
        await kit.RunAsync(services => services.GetRequiredService<ITicketService>().ReplyAsync(byReply, new TicketReply
        {
            Body = "Fixed it.",
            Author = new CrestApps.OrchardCore.Parties.Models.PartyRef(CrestApps.OrchardCore.Parties.Models.PartyKinds.User, "ben"),
            AuthorName = "Ben",
            SetStatusId = TicketKit.Solved.ItemId,
        }, TestContext.Current.CancellationToken));
        var solvers = await kit.ReadAsync(async services => (await services.GetRequiredService<ISession>().QueryIndex<TicketIndex>(index => index.SolvedById != null).ListAsync()).ToDictionary(index => index.ContentItemId, index => index.SolvedById));

        // Assert
        Assert.Equal("ana", (await kit.GetAsync(byUpdate)).GetOrCreate<TicketPart>().SolvedById);
        Assert.Equal("ana", solvers[byUpdate]);
        Assert.Equal("ben", solvers[byReply]);
    }

    private static Task<TicketResult> UpdateAsync(TicketKit kit, string ticketId, TicketUpdate update, TicketActor actor = null)
        => kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, update, actor, TestContext.Current.CancellationToken));

    private static async Task<ContentItem> CreateAsync(TicketKit kit, string priorityId)
    {
        var result = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().CreateAsync(new TicketCreateRequest
        {
            Subject = "Server down",
            Body = "Nobody can sign in.",
            DeskId = "support",
            PriorityId = priorityId,
            Impact = TicketLevel.High,
            Urgency = TicketLevel.Medium,
            Channel = TicketsConstants.Channels.Agent,
        }, TestContext.Current.CancellationToken));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Ticket;
    }
}
