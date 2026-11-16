using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Notifications;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Security;
using OrchardCore.Security.AuthorizationHandlers;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Ticket notifications are typed: each names its type (a row of the user's notification preferences), the ticket and
/// the page it opens, so users choose per type and the notification center filters and links them. A ticket
/// notification is hidden from a user who may no longer see the ticket (with the real authorization service and Orchard
/// Core's permission handler), such as after the ticket moved to a desk they don't work on.
/// </summary>
public sealed class TicketNotificationTypesTests
{
    [Fact]
    public async Task ATicketNotification_IsHidden_OnceTheUserMayNoLongerSeeTheTicket()
    {
        // Arrange: an agent of the support desk; t1 is on support, t4 on HR, and "gone" was deleted.
        await using var harness = await TicketReportDataSourceTests.CreateAsync(RealAccess);
        await TicketReportDataSourceTests.SeedAsync(harness);
        var agent = CalendarTestHarness.User("ana", TicketsPermissions.ForDesk(TicketOperation.ViewAll, new TicketDesk { ItemId = "support", Name = "Support" }).Name);

        // Act
        var before = await harness.RunAsync(services => EvaluateAsync(services, agent, "t1", "t4", "gone"), commit: false);
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var ticket = await session.Query<ContentItem, TicketIndex>(index => index.ContentItemId == "t1").FirstOrDefaultAsync();
            ticket.Alter<TicketPart>(part => part.DeskId = "hr");
            await session.SaveAsync(ticket);

            return true;
        });
        var after = await harness.RunAsync(services => EvaluateAsync(services, agent, "t1"), commit: false);

        // Assert
        Assert.Equal([NotificationVisibility.Visible, NotificationVisibility.Hidden, NotificationVisibility.Hidden], before);
        Assert.Equal([NotificationVisibility.Hidden], after);
    }

    [Fact]
    public async Task AnApprover_KeepsTheApprovalNotice_WithoutSeeingTheDesk()
    {
        // Arrange: t4 is on HR and waits for maria's approval; maria doesn't work on HR.
        await using var harness = await TicketReportDataSourceTests.CreateAsync(RealAccess);
        await TicketReportDataSourceTests.SeedAsync(harness);
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var ticket = await session.Query<ContentItem, TicketIndex>(index => index.ContentItemId == "t4").FirstOrDefaultAsync();
            ticket.Alter<TicketPart>(part => part.Approval = new TicketApproval { State = TicketApprovalState.Pending, Steps = [new TicketApprovalStep { Name = "Manager", ApproverIds = ["maria"] }] });
            await session.SaveAsync(ticket);

            return true;
        });
        var maria = CalendarTestHarness.User("maria");

        // Act
        var visibility = await harness.RunAsync(async services =>
        {
            var approval = new NotificationVisibilityContext(new Notification { UserId = "maria", Type = TicketsConstants.NotificationTypes.ApprovalRequested, ReferenceId = "t4" }, maria);
            var assigned = new NotificationVisibilityContext(new Notification { UserId = "maria", Type = TicketsConstants.NotificationTypes.Assigned, ReferenceId = "t4" }, maria);
            await ActivatorUtilities.CreateInstance<TicketNotificationVisibilityHandler>(services).EvaluateAsync([approval, assigned]);

            return (approval.Visibility, assigned.Visibility);
        }, commit: false);

        // Assert
        Assert.Equal((NotificationVisibility.Visible, NotificationVisibility.Hidden), visibility);
    }

    [Fact]
    public async Task TheAssignmentNotice_NamesItsType_TheTicket_AndThePageItOpens_AndSkipsWhoAssignedIt()
    {
        // Arrange
        var (sender, sent) = Sender(signedIn: "boss");
        var desks = new Mock<CrestApps.Core.Services.ICatalog<TicketDesk>>();
        desks.Setup(catalog => catalog.FindByIdAsync("support", It.IsAny<CancellationToken>())).ReturnsAsync(new TicketDesk { ItemId = "support", NotifyAssignee = true });
        var notifier = new TicketAssignmentNotifier(desks.Object, sender, new PassThroughStringLocalizer<TicketAssignmentNotifier>());

        // Act
        await notifier.AssignedAsync(Ticket("t1", assigneeId: "ana"), null, TestContext.Current.CancellationToken);
        await notifier.AssignedAsync(Ticket("t2", assigneeId: "boss"), null, TestContext.Current.CancellationToken);

        // Assert
        var (to, message) = Assert.Single(sent);
        Assert.Equal("ana", to);
        Assert.Equal(TicketsConstants.NotificationTypes.Assigned, message.Type);
        Assert.Equal("t1", message.ReferenceId);
        Assert.Equal("~/Admin/ticket/t1", message.Url);
        Assert.Equal("A ticket was assigned to you: T-t1 Printer jam", message.Subject);
        Assert.Equal("T-t1", ((NotificationMessage)message).GetOrCreate<TicketNotificationInfo>().Number);
    }

    internal static (TicketNotificationSender Sender, List<(string To, INotificationMessage Message)> Sent) Sender(string signedIn = null, IEnumerable<Notification> unread = null)
    {
        var sent = new List<(string To, INotificationMessage Message)>();
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((string id) => new TestUser { UserName = id });
        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object to, INotificationMessage message, CancellationToken _) => sent.Add((((TestUser)to).UserName, message)))
            .ReturnsAsync(new NotificationSendResult());
        var manager = new Mock<INotificationManager>();
        manager.Setup(value => value.ListAsync(It.IsAny<NotificationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationFilter filter, CancellationToken _) => [.. (unread ?? []).Concat(sent.Select(item => Stored(item.To, item.Message)))
                .Where(notification => notification.UserId == filter.UserId && notification.Type == filter.Type && notification.ReferenceId == filter.ReferenceId)]);
        var httpContext = new DefaultHttpContext();

        if (signedIn is not null)
        {
            httpContext.User = CalendarTestHarness.User(signedIn);
        }

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(value => value.HttpContext).Returns(httpContext);

        return (new TicketNotificationSender(userManager.Object, notifications.Object, manager.Object, accessor.Object, Options.Create(new AdminOptions()), new PassThroughStringLocalizer<TicketNotificationSender>()), sent);
    }

    internal static ContentItem Ticket(string id, string assigneeId = null, string teamId = null, string deskId = "support")
    {
        var ticket = new ContentItem { ContentItemId = id, ContentType = TicketsConstants.DefaultContentType, DisplayText = "Printer jam" };
        ticket.Apply(new TicketPart { Number = "T-" + id, DeskId = deskId, AssigneeId = assigneeId, TeamId = teamId, Requester = new PartyRef(PartyKinds.User, "requester") });

        return ticket;
    }

    // What the notification service keeps from a message, for the sender's check of what a user already has.
    private static Notification Stored(string userId, INotificationMessage message)
    {
        var notification = new Notification { UserId = userId, Type = message.Type, ReferenceId = message.ReferenceId, Subject = message.Subject };

        if (message is NotificationMessage entity && entity.TryGet<TicketNotificationInfo>(out var info))
        {
            notification.Put(info);
        }

        return notification;
    }

    private static async Task<NotificationVisibility[]> EvaluateAsync(IServiceProvider services, ClaimsPrincipal user, params string[] ticketIds)
    {
        var contexts = ticketIds
            .Select(id => new NotificationVisibilityContext(new Notification { UserId = "ana", Type = TicketsConstants.NotificationTypes.Assigned, ReferenceId = id }, user))
            .ToList();

        await ActivatorUtilities.CreateInstance<TicketNotificationVisibilityHandler>(services).EvaluateAsync(contexts);

        return [.. contexts.Select(context => context.Visibility)];
    }

    // The real ticket access service, on the real authorization service with Orchard Core's permission handler.
    private static void RealAccess(IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddSingleton<IPermissionGrantingService, DefaultPermissionGrantingService>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<ITicketAccessService, DefaultTicketAccessService>();
    }

    private sealed class TestUser : IUser
    {
        public string UserName { get; set; }
    }
}
