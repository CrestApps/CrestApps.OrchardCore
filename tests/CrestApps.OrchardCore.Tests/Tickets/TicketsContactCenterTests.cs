using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.ContactCenter;
using CrestApps.OrchardCore.Tickets.ContactCenter.Models;
using CrestApps.OrchardCore.Tickets.ContactCenter.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The Contact Center bridge: a desk's new, unassigned ticket becomes a task in the desk's queue, linked both ways; the
/// agent who accepts the offer gets the ticket (once, and not when someone already has it); and work nobody took yet is
/// withdrawn when the ticket is given to someone by hand, solved, closed or merged.
/// </summary>
public sealed class TicketsContactCenterTests
{
    [Fact]
    public async Task ANewUnassignedTicket_IsOfferedThroughTheDesksQueue()
    {
        // Arrange
        var kit = new Kit();

        // Act
        await kit.Router.CreatedAsync(Ticket(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(kit.Created);
        Assert.Equal(ActivityKind.Task, kit.Created.Kind);
        Assert.Equal("t1", kit.Created.GetOrCreate<TicketActivityLink>().TicketId);
        Assert.Equal("T-1 Printer jams", kit.Created.Instructions);
        kit.Queue.Verify(service => service.EnqueueAsync("a1", "q1", InteractionPriority.High, It.IsAny<CancellationToken>()), Times.Once);
        kit.Tickets.Verify(service => service.LinkAsync("t1", new RecordRef(TicketsContactCenterConstants.ActivityRecordKind, "a1"), TicketLinkKinds.Related, It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("assigned")]
    [InlineData("approval")]
    [InlineData("no-queue")]
    public async Task ATicketSomeoneHas_OneWaitingForApproval_OrOnADeskWithoutAQueue_IsntOffered(string why)
    {
        // Arrange
        var kit = new Kit();
        var ticket = Ticket(deskId: why == "no-queue" ? "other" : "support");
        ticket.Alter<TicketPart>(part =>
        {
            part.AssigneeId = why == "assigned" ? "ana" : null;
            part.Approval = why == "approval" ? new TicketApproval { State = TicketApprovalState.Pending } : null;
        });

        // Act
        await kit.Router.CreatedAsync(ticket, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(kit.Created);
        kit.Queue.Verify(service => service.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueuedWork_IsWithdrawn_WhenTheTicketIsGivenByHandSolvedOrMerged_ButNotWhenItsOfferIsAccepted()
    {
        // Arrange
        var kit = new Kit();
        var ticket = Ticket();
        ticket.Alter<TicketPart>(part =>
        {
            part.AssigneeId = "ana";
            part.Links = [new TicketLink { Record = new RecordRef(TicketsContactCenterConstants.ActivityRecordKind, "a1"), Kind = TicketLinkKinds.Related }];
        });
        var ct = TestContext.Current.CancellationToken;

        // Act
        using (TicketEventActor.Use(new TicketActor { Name = TicketQueueRouter.OfferAcceptedActorName }))
        {
            await kit.Router.AssignedAsync(ticket, null, ct);
        }

        kit.Withdrawal.Verify(service => service.WithdrawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ContactCenterActor>(), It.IsAny<CancellationToken>()), Times.Never);

        using (TicketEventActor.Use(TicketActor.User("ben", "Ben")))
        {
            await kit.Router.AssignedAsync(ticket, null, ct);
        }

        await kit.Router.StatusChangedAsync(ticket, new TicketStatus { Category = TicketStatusCategory.Open }, new TicketStatus { Category = TicketStatusCategory.Solved }, ct);
        await kit.Router.StatusChangedAsync(ticket, new TicketStatus { Category = TicketStatusCategory.New }, new TicketStatus { Category = TicketStatusCategory.Open }, ct);
        await kit.Router.MergedAsync(ticket, Ticket(), ct);

        // Assert
        kit.Withdrawal.Verify(service => service.WithdrawAsync("a1", It.IsAny<string>(), ContactCenterActor.System, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("u-agent", false)]
    [InlineData("ana", false)]
    public async Task TheAgentWhoAcceptsTheOffer_GetsTheTicket_OnlyIfNobodyHasIt_CheckedInTheLock(string assignee, bool assigns)
    {
        // Arrange
        var kit = new Kit();
        var ticket = Ticket();
        ticket.Alter<TicketPart>(part => part.AssigneeId = assignee);
        kit.Tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(ticket);
        var handler = kit.OfferHandler();
        var other = new InteractionEvent { EventType = ContactCenterConstants.Events.QueueItemAdded };

        // Act
        await handler.HandleAsync(Accepted(), TestContext.Current.CancellationToken);
        await handler.HandleAsync(other, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(ContactCenterHandlerReplaySafety.Unspecified, handler.ReplaySafety);
        kit.Tickets.Verify(service => service.UpdateAsync("t1", It.Is<TicketUpdate>(update => update.AssigneeId == "u-agent" && update.OnlyIfUnassigned), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), assigns ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task ATicketTakenBeforeTheOfferWasAccepted_StaysWithItsAgent_AndTheWorkIsWithdrawn()
    {
        // Arrange: Ana took the ticket from its page before the accept event was handled.
        var kit = new Kit();
        var ticket = Ticket();
        ticket.Alter<TicketPart>(part => part.AssigneeId = "ana");
        kit.Tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(ticket);

        // Act
        await kit.OfferHandler().HandleAsync(Accepted(), TestContext.Current.CancellationToken);

        // Assert
        kit.Tickets.Verify(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Never);
        kit.Withdrawal.Verify(service => service.WithdrawAsync("a1", It.IsAny<string>(), ContactCenterActor.System, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ATicketTakenBetweenTheReadAndTheAssignment_IsRefusedInTheLock_AndTheWorkIsWithdrawn()
    {
        // Arrange: the handler reads the ticket unassigned; by the time its update holds the lock, Ana has it (or the
        // agent turns out not to be one who may work the desk), so the ticket service refuses the conditional update.
        var kit = new Kit();
        kit.Tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(Ticket());
        kit.Tickets.Setup(service => service.UpdateAsync("t1", It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketResult { Errors = ["Someone else already has this ticket."] });

        // Act
        await kit.OfferHandler().HandleAsync(Accepted(), TestContext.Current.CancellationToken);

        // Assert
        kit.Withdrawal.Verify(service => service.WithdrawAsync("a1", It.Is<string>(reason => reason.Contains("Someone else already has this ticket.", StringComparison.Ordinal)), ContactCenterActor.System, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("gone")]
    [InlineData("off")]
    public async Task ADeskWhoseQueueIsGoneOrTurnedOff_OffersNothing(string queueId)
    {
        // Arrange: deleted or turned off after the desk named it, or named by a recipe from another site.
        var kit = new Kit(queueId);

        // Act
        await kit.Router.CreatedAsync(Ticket(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(kit.Created);
        kit.Queue.Verify(service => service.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void TheRouter_DoesNotInjectWhatLeadsBackToTheTicketService()
    {
        // The ticket service constructs its observers. The queue and withdrawal services publish through the event outbox,
        // which constructs every event handler, and the offer handler uses the ticket service: injecting any of the three
        // into the router is a construction cycle the site reports on the first ticket page.
        var injected = typeof(TicketQueueRouter).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.DoesNotContain(typeof(ITicketService), injected);
        Assert.DoesNotContain(typeof(IActivityQueueService), injected);
        Assert.DoesNotContain(typeof(IQueuedWorkWithdrawalService), injected);
    }

    [Theory]
    [InlineData(1, InteractionPriority.Low)]
    [InlineData(2, InteractionPriority.Normal)]
    [InlineData(3, InteractionPriority.High)]
    [InlineData(5, InteractionPriority.Highest)]
    public void ATicketsPriority_IsTheQueuePriority(int rank, InteractionPriority priority)
        => Assert.Equal(priority, TicketQueueRouter.PriorityOf(rank));

    private static InteractionEvent Accepted()
    {
        var accepted = new InteractionEvent { EventType = ContactCenterConstants.Events.QueueItemAssigned };
        accepted.SetData(new OfferLifecycleEventData { ActivityItemId = "a1", UserId = "u-agent", QueueId = "q1" });

        return accepted;
    }

    private static ContentItem Ticket(string deskId = "support")
    {
        var ticket = new ContentItem { ContentItemId = "t1", ContentType = TicketsConstants.DefaultContentType, DisplayText = "Printer jams" };
        ticket.Apply(new TicketPart { Number = "T-1", DeskId = deskId, PriorityRank = 3 });

        return ticket;
    }

    private sealed class Kit
    {
        public Kit(string queueId = "q1")
        {
            var support = new TicketDesk { ItemId = "support", Name = "Support" };
            support.Put(new TicketDeskQueueSettings { QueueId = queueId });
            var queues = new Mock<IActivityQueueManager>();
            queues.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => id switch
                {
                    "q1" => new ActivityQueue { ItemId = "q1", Name = "Help desk" },
                    "off" => new ActivityQueue { ItemId = "off", Name = "Old", Enabled = false },
                    _ => null,
                });
            var desks = new Mock<ICatalog<TicketDesk>>();
            desks.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => id == "support" ? support : new TicketDesk { ItemId = id, Name = id });

            Activities.Setup(value => value.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new OmnichannelActivity { ItemId = "a1" });
            Activities.Setup(value => value.CreateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
                .Callback((OmnichannelActivity activity, CancellationToken _) => Created = activity)
                .Returns(ValueTask.CompletedTask);
            Activities.Setup(value => value.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var activity = new OmnichannelActivity { ItemId = "a1" };
                    activity.Put(new TicketActivityLink { TicketId = "t1" });

                    return activity;
                });
            Tickets.Setup(value => value.LinkAsync(It.IsAny<string>(), It.IsAny<RecordRef>(), It.IsAny<string>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TicketResult());
            Tickets.Setup(value => value.UpdateAsync(It.IsAny<string>(), It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TicketResult());
            Withdrawal.Setup(value => value.WithdrawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ContactCenterActor>(), It.IsAny<CancellationToken>())).ReturnsAsync(QueuedWorkWithdrawalOutcome.Withdrawn);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc));
            Services = new ServiceCollection()
                .AddSingleton(queues.Object)
                .AddSingleton(Queue.Object)
                .AddSingleton(Tickets.Object)
                .AddSingleton(Withdrawal.Object)
                .BuildServiceProvider();

            Router = new TicketQueueRouter(Services, Activities.Object, desks.Object, clock.Object, NullLogger<TicketQueueRouter>.Instance);
        }

        public Mock<IOmnichannelActivityManager> Activities { get; } = new();

        public Mock<IActivityQueueService> Queue { get; } = new();

        public Mock<ITicketService> Tickets { get; } = new();

        public Mock<IQueuedWorkWithdrawalService> Withdrawal { get; } = new();

        public OmnichannelActivity Created { get; private set; }

        public TicketQueueRouter Router { get; }

        public IServiceProvider Services { get; }

        public TicketOfferAcceptedHandler OfferHandler()
            => new(Activities.Object, Tickets.Object, Services, NullLogger<TicketOfferAcceptedHandler>.Instance);
    }
}
