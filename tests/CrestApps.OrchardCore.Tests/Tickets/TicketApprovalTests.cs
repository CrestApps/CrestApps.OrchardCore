using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A ticket type can ask for approval: the ticket waits, step by step, for one approver of each step; only a step's
/// approvers decide it; the last approval sends the ticket back to work; a refusal solves it and tells the requester why.
/// </summary>
public sealed class TicketApprovalTests
{
    [Fact]
    public async Task EachStep_IsDecidedByItsApprovers_AndTheLastApprovalSendsTheTicketToWork()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync("New laptop", "Mine is broken.")).Ticket.ContentItemId;

        // Act
        var started = await kit.RunAsync(services => Tickets(services).StartApprovalAsync(ticketId,
        [
            new TicketApprovalStep { Name = "Manager", ApproverIds = ["mia"] },
            new TicketApprovalStep { Name = "Nobody", ApproverIds = [] },
            new TicketApprovalStep { Name = "Finance", ApproverIds = ["fred", "fay"] },
        ], null, TicketActor.Site, TestContext.Current.CancellationToken));
        var waiting = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        var wrongPerson = await kit.RunAsync(services => Tickets(services).DecideApprovalAsync(ticketId, "fred", "Fred", true, null, TestContext.Current.CancellationToken));
        var byManager = await kit.RunAsync(services => Tickets(services).DecideApprovalAsync(ticketId, "mia", "Mia", true, null, TestContext.Current.CancellationToken));
        var afterManager = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        var byFinance = await kit.RunAsync(services => Tickets(services).DecideApprovalAsync(ticketId, "fay", "Fay", true, "Within budget.", TestContext.Current.CancellationToken));
        var approved = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        var again = await kit.RunAsync(services => Tickets(services).DecideApprovalAsync(ticketId, "fay", "Fay", true, null, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(started.Succeeded, string.Join(" ", started.Errors));
        Assert.Equal(TicketKit.Pending.ItemId, waiting.StatusId);
        Assert.Equal(TicketApprovalState.Pending, waiting.Approval.State);
        Assert.Equal(["Manager", "Finance"], waiting.Approval.Steps.Select(step => step.Name));
        Assert.Equal(TicketKit.New.ItemId, waiting.Approval.HeldStatusId);
        Assert.False(wrongPerson.Succeeded);
        Assert.True(byManager.Succeeded, string.Join(" ", byManager.Errors));
        Assert.Equal("Finance", afterManager.Approval.Current.Name);
        Assert.True(byFinance.Succeeded, string.Join(" ", byFinance.Errors));
        Assert.Equal(TicketApprovalState.Approved, approved.Approval.State);
        Assert.Equal("Fay", approved.Approval.Steps[1].DecidedByName);
        Assert.Equal("Within budget.", approved.Approval.Steps[1].Comment);

        // New can't come back from waiting, so the approved request goes to work.
        Assert.Equal(TicketKit.Open.ItemId, approved.StatusId);
        Assert.False(again.Succeeded);
        Assert.Contains("ApprovalRequested:Manager", kit.Observers.Events);
        Assert.Contains("ApprovalRequested:Finance", kit.Observers.Events);
    }

    [Fact]
    public async Task ARefusal_NeedsAReason_SolvesTheTicket_AndTellsTheRequester()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync("New laptop", "Mine is broken.")).Ticket.ContentItemId;
        await kit.RunAsync(services => Tickets(services).StartApprovalAsync(ticketId, [new TicketApprovalStep { Name = "Manager", ApproverIds = ["mia"] }], null, TicketActor.Site, TestContext.Current.CancellationToken));

        // Act
        var noReason = await kit.RunAsync(services => Tickets(services).DecideApprovalAsync(ticketId, "mia", "Mia", false, " ", TestContext.Current.CancellationToken));
        var refused = await kit.RunAsync(services => Tickets(services).DecideApprovalAsync(ticketId, "mia", "Mia", false, "Laptops are replaced in March.", TestContext.Current.CancellationToken));
        var part = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        var entries = await kit.EntriesAsync(ticketId);

        // Assert
        Assert.False(noReason.Succeeded);
        Assert.True(refused.Succeeded, string.Join(" ", refused.Errors));
        Assert.Equal(TicketApprovalState.Rejected, part.Approval.State);
        Assert.Equal(TicketKit.Solved.ItemId, part.StatusId);
        var told = Assert.Single(entries, entry => entry.Visibility == TimelineVisibility.Public && entry.Body.Contains("Laptops are replaced in March.", StringComparison.Ordinal));
        Assert.Equal("Mia", told.AuthorName);
        Assert.Contains("AgentReplied", kit.Observers.Events);
    }

    [Fact]
    public async Task ANewTicket_WaitsForItsTypesSteps_WithTheRequestersManager()
    {
        // Arrange
        var settings = new TicketApprovalSettings
        {
            WaitingStatusId = "approval",
            Steps =
            [
                new TicketApprovalStepDefinition { Name = "Manager", RequesterManager = true },
                new TicketApprovalStepDefinition { Name = "IT", ApproverIds = ["ivy"] },
                new TicketApprovalStepDefinition { Name = "Empty" },
            ],
        };
        var definitions = Definitions(settings);
        var tickets = new Mock<ITicketService>();
        IReadOnlyList<TicketApprovalStep> started = null;
        tickets.Setup(service => service.StartApprovalAsync("t1", It.IsAny<IReadOnlyList<TicketApprovalStep>>(), "approval", It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()))
            .Callback((string _, IReadOnlyList<TicketApprovalStep> steps, string _, TicketActor _, CancellationToken _) => started = steps)
            .ReturnsAsync(new TicketResult());
        var managers = new Mock<ITicketRequesterManagerSource>();
        managers.Setup(source => source.FindManagerUserIdAsync("dana", It.IsAny<CancellationToken>())).ReturnsAsync("mia");
        var services = new ServiceCollection().AddSingleton(tickets.Object).BuildServiceProvider();
        var starter = new TicketApprovalStarter(definitions, services, [managers.Object]);
        var withoutSteps = new TicketApprovalStarter(Definitions(new TicketApprovalSettings()), services, [managers.Object]);

        // Act
        await starter.CreatedAsync(Ticket("t1"), TestContext.Current.CancellationToken);
        await withoutSteps.CreatedAsync(Ticket("t2"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Manager", "IT"], started.Select(step => step.Name));
        Assert.Equal(["mia"], started[0].ApproverIds);
        Assert.Equal(["ivy"], started[1].ApproverIds);
        tickets.Verify(service => service.StartApprovalAsync("t2", It.IsAny<IReadOnlyList<TicketApprovalStep>>(), It.IsAny<string>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(TicketDeskAudience.Customers, false, true)]
    [InlineData(TicketDeskAudience.Internal, false, false)]
    [InlineData(TicketDeskAudience.Internal, true, true)]
    public void ThePortalsCatalog_OffersInternalDesks_OnlyToStaff(TicketDeskAudience audience, bool isStaff, bool offered)
        => Assert.Equal(offered, CrestApps.OrchardCore.Tickets.Portal.TicketPortalAccess.Offers(new TicketDesk { ItemId = "d", Name = "D", Audience = audience }, isStaff));

    private static ITicketService Tickets(IServiceProvider services)
        => services.GetRequiredService<ITicketService>();

    private static ContentItem Ticket(string id)
    {
        var ticket = new ContentItem { ContentItemId = id, ContentType = TicketsConstants.DefaultContentType };
        ticket.Apply(new TicketPart { Requester = new PartyRef(PartyKinds.User, "dana") });

        return ticket;
    }

    private static IContentDefinitionManager Definitions(TicketApprovalSettings settings)
    {
        var typePart = new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName, [], []), new System.Text.Json.Nodes.JsonObject
        {
            [nameof(TicketApprovalSettings)] = System.Text.Json.JsonSerializer.SerializeToNode(settings),
        });
        var type = new ContentTypeDefinition(TicketsConstants.DefaultContentType, TicketsConstants.DefaultContentType, [typePart], []);
        var manager = new Mock<IContentDefinitionManager>();
        manager.Setup(value => value.GetTypeDefinitionAsync(TicketsConstants.DefaultContentType)).ReturnsAsync(type);

        return manager.Object;
    }
}
