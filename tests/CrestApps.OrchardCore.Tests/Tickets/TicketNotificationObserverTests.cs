using CrestApps.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Notifications;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Notifications;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// What agents hear about their tickets, each as its own type, and what they don't: their own changes, a note that
/// already told them as a mention, the requester's reply that reopens the ticket (told as the reply), a new ticket's
/// first status, and the same unread service-level notice twice.
/// </summary>
public sealed class TicketNotificationObserverTests
{
    [Fact]
    public async Task ATicketGivenToATeam_TellsItsMembers_ButNotTheAssigneeOrWhoGaveIt()
    {
        // Arrange: boss gives the ticket to team A (ana, ben, boss), assigned to ana.
        var (observer, sent) = Create(signedIn: "boss");
        var ticket = TicketNotificationTypesTests.Ticket("t1", assigneeId: "ana", teamId: "teamA");

        // Act
        await observer.UpdatedAsync(ticket, [new TimelineFieldChange { Key = TicketChangeKeys.Team, ToValue = "teamA" }], TestContext.Current.CancellationToken);
        await observer.UpdatedAsync(ticket, [new TimelineFieldChange { Key = TicketChangeKeys.Subject, To = "New subject" }], TestContext.Current.CancellationToken);

        // Assert
        var (to, message) = Assert.Single(sent);
        Assert.Equal("ben", to);
        Assert.Equal(TicketsConstants.NotificationTypes.TeamAssigned, message.Type);
    }

    [Fact]
    public async Task TheRequestersReply_TellsTheAssignee_OrTheTeamWhenNobodyHasTheTicket()
    {
        // Arrange
        var (observer, sent) = Create();
        var reply = new TimelineEntry { Kind = TicketsConstants.EntryKinds.Reply, Visibility = TimelineVisibility.Public };

        // Act
        await observer.RepliedAsync(TicketNotificationTypesTests.Ticket("t1", assigneeId: "ana", teamId: "teamA"), reply, fromRequester: true, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(TicketNotificationTypesTests.Ticket("t2", teamId: "teamA"), reply, fromRequester: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([("ana", "t1"), ("ana", "t2"), ("ben", "t2"), ("boss", "t2")], sent.Select(item => (item.To, item.Message.ReferenceId)));
        Assert.All(sent, item => Assert.Equal(TicketsConstants.NotificationTypes.RequesterReplied, item.Message.Type));
    }

    [Fact]
    public async Task ANote_TellsTheAssignee_UnlessItMentionsThem_OrTheyWroteIt()
    {
        // Arrange
        var (observer, sent) = Create(signedIn: "ben");
        var ticket = TicketNotificationTypesTests.Ticket("t1", assigneeId: "ana");
        var (byAna, anaSent) = Create(signedIn: "ana");

        // Act
        await observer.RepliedAsync(ticket, Note("Called the vendor."), fromRequester: false, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(ticket, Note("@ana can you check?"), fromRequester: false, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(ticket, Note("@anabel has the logs."), fromRequester: false, TestContext.Current.CancellationToken);
        await byAna.RepliedAsync(ticket, Note("Called the vendor."), fromRequester: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, sent.Count);
        Assert.All(sent, item => Assert.Equal(("ana", TicketsConstants.NotificationTypes.NoteAdded), (item.To, item.Message.Type)));
        Assert.Empty(anaSent);
    }

    [Fact]
    public async Task AStatusChange_TellsTheAssignee_OnlyWhenSomeoneElseMadeIt()
    {
        // Arrange
        var (byBen, benSent) = Create(signedIn: "ben");
        var (byAna, anaSent) = Create(signedIn: "ana");
        var (bySite, siteSent) = Create();
        var ticket = TicketNotificationTypesTests.Ticket("t1", assigneeId: "ana");
        var open = new TicketStatus { ItemId = "open", Name = "Open" };
        var pending = new TicketStatus { ItemId = "pending", Name = "Pending" };

        // Act: ben sets it (with his reply or alone), ana sets her own, the first status of a new ticket, and the
        // requester's reply that reopens it.
        await byBen.StatusChangedAsync(ticket, open, pending, TestContext.Current.CancellationToken);
        await byAna.StatusChangedAsync(ticket, open, pending, TestContext.Current.CancellationToken);
        await bySite.StatusChangedAsync(ticket, null, open, TestContext.Current.CancellationToken);

        using (TicketEventActor.Use(new TicketActor { Party = new PartyRef(PartyKinds.User, "requester"), Name = "Requester" }))
        {
            await bySite.StatusChangedAsync(ticket, pending, open, TestContext.Current.CancellationToken);
        }

        // Assert
        var (to, message) = Assert.Single(benSent);
        Assert.Equal(("ana", "Your ticket is now Pending: T-t1 Printer jam"), (to, message.Subject));
        Assert.Empty(anaSent);
        Assert.Empty(siteSent);
    }

    [Fact]
    public async Task AServiceLevelNotice_IsSentOnce_WhileUnread_AndTheBreachAfterIt()
    {
        // Arrange
        var (observer, sent) = Create();
        var ticket = TicketNotificationTypesTests.Ticket("t1", assigneeId: "ana");

        // Act: the warning twice (a second monitor run, a retargeted timer), then the breach twice.
        await observer.ServiceLevelAsync(ticket, "FirstResponse", breached: false, TestContext.Current.CancellationToken);
        await observer.ServiceLevelAsync(ticket, "FirstResponse", breached: false, TestContext.Current.CancellationToken);
        await observer.ServiceLevelAsync(ticket, "FirstResponse", breached: true, TestContext.Current.CancellationToken);
        await observer.ServiceLevelAsync(ticket, "FirstResponse", breached: true, TestContext.Current.CancellationToken);
        await observer.ServiceLevelAsync(ticket, "Resolution", breached: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["A service-level target is due soon: T-t1 Printer jam", "A service-level target was missed: T-t1 Printer jam", "A service-level target is due soon: T-t1 Printer jam"],
            sent.Select(item => item.Message.Subject));
        Assert.All(sent, item => Assert.Equal(("ana", TicketsConstants.NotificationTypes.ServiceLevel), (item.To, item.Message.Type)));
    }

    [Fact]
    public async Task AMerge_TellsTheAssigneeOfTheTicketItWentInto()
    {
        // Arrange
        var (observer, sent) = Create();

        // Act
        await observer.MergedAsync(TicketNotificationTypesTests.Ticket("t2"), TicketNotificationTypesTests.Ticket("t1", assigneeId: "ana"), TestContext.Current.CancellationToken);

        // Assert
        var (to, message) = Assert.Single(sent);
        Assert.Equal(("ana", "t1", "Ticket T-t2 was merged into yours: T-t1 Printer jam"), (to, message.ReferenceId, message.Subject));
    }

    [Fact]
    public async Task AnAgentsPublicReply_TellsARequesterWhoSignsIn_InTheNotificationCenter()
    {
        // Arrange
        var (observer, sent) = Create(signedIn: "ana");
        var ticket = TicketNotificationTypesTests.Ticket("t1", assigneeId: "ana");

        // Act
        await observer.RepliedAsync(ticket, new TimelineEntry { Kind = TicketsConstants.EntryKinds.Reply, Visibility = TimelineVisibility.Public }, fromRequester: false, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(ticket, new TimelineEntry { Kind = TicketsConstants.EntryKinds.Reply, Visibility = TimelineVisibility.Internal }, fromRequester: false, TestContext.Current.CancellationToken);

        // Assert
        var (to, message) = Assert.Single(sent);
        Assert.Equal(("requester", TicketsConstants.NotificationTypes.Replied, "~/account/requests/T-t1"), (to, message.Type, message.Url));
    }

    private static TimelineEntry Note(string body)
        => new() { Kind = TicketsConstants.EntryKinds.Note, Visibility = TimelineVisibility.Internal, Body = body };

    private static (TicketNotificationObserver Observer, List<(string To, INotificationMessage Message)> Sent) Create(string signedIn = null)
    {
        var (sender, sent) = TicketNotificationTypesTests.Sender(signedIn);
        var desks = new Mock<ICatalog<TicketDesk>>();
        desks.Setup(catalog => catalog.FindByIdAsync("support", It.IsAny<CancellationToken>())).ReturnsAsync(new TicketDesk { ItemId = "support", NotifyAssignee = true });
        var memberships = new Mock<ITeamMembershipService>();
        memberships.Setup(service => service.GetMembersAsync("teamA", It.IsAny<bool>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TeamMember { TeamId = "teamA", UserId = "ana" }, new TeamMember { TeamId = "teamA", UserId = "ben" }, new TeamMember { TeamId = "teamA", UserId = "boss" }]);
        var services = new ServiceCollection().AddSingleton(memberships.Object).BuildServiceProvider();
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((string id) => new NamedUser { UserName = id });

        return (new TicketNotificationObserver(sender, desks.Object, userManager.Object, services, new PassThroughStringLocalizer<TicketNotificationObserver>()), sent);
    }

    private sealed class NamedUser : IUser
    {
        public string UserName { get; set; }
    }
}
