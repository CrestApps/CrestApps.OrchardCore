using System.Security.Claims;
using CrestApps.OrchardCore.Orders;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.WorkOrders;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Notifications;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Notifications;
using OrchardCore.Security;
using OrchardCore.Users;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// The work order and order timelines are read by the people who see those records, take internal notes from the people
/// who manage them, and only on a record that exists. A mention tells a colleague who may read the entry, with a link
/// and an encoded title but never the entry's text, and never the author or someone who may not read it.
/// </summary>
public sealed class TimelineProvidersAndMentionsTests
{
    [Fact]
    public async Task AWorkOrdersTimeline_TakesNotes_OnlyOnAWorkOrderThatExists()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(
            services => services.AddWorkOrdersWithServiceLevels(),
            WorkOrderTestStores.CreateSchemaAsync,
            [.. WorkOrderTestStores.IndexProviders],
            WorkOrderTestStores.Collections);
        await harness.RunAsync(services => services.GetRequiredService<ISession>().SaveAsync(new WorkOrder { ItemId = "wo1", Number = "WO-1", Summary = "Boiler" }, collection: WorkOrdersConstants.CollectionName));
        var manager = Manager(WorkOrdersPermissions.ManageWorkOrders.Name, WorkOrdersPermissions.ViewWorkOrders.Name);

        // Act
        var (onExisting, onMissing, comment) = await harness.RunAsync(async services =>
        {
            var access = new WorkOrderTimelineAccess(Authorization(), services.GetRequiredService<WorkOrderStore>());

            return (
                await access.CanAddAsync(manager, new RecordRef(WorkOrdersConstants.RecordKind, "wo1"), TimelineConstants.Kinds.Note, TimelineVisibility.Internal),
                await access.CanAddAsync(manager, new RecordRef(WorkOrdersConstants.RecordKind, "made-up"), TimelineConstants.Kinds.Note, TimelineVisibility.Internal),
                await access.CanAddAsync(manager, new RecordRef(WorkOrdersConstants.RecordKind, "wo1"), TimelineConstants.Kinds.Comment, TimelineVisibility.Public));
        }, commit: false);

        // Assert
        Assert.True(onExisting);
        Assert.False(onMissing);
        Assert.False(comment);
    }

    [Fact]
    public async Task AnOrdersTimeline_IsForThoseWhoManageOrders_AndTakesNotesOnlyOnAnOrderThatExists()
    {
        // Arrange
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync("order1", It.IsAny<CancellationToken>())).ReturnsAsync(new Order { ItemId = "order1" });
        var access = new OrderTimelineAccess(Authorization(), orders.Object);
        var manager = Manager(OrderPermissions.ManageOrders.Name);
        var stranger = Manager();

        // Act and assert
        Assert.True(await access.CanViewAsync(manager, new RecordRef(OrderConstants.RecordKind, "order1"), TimelineVisibility.Internal, TestContext.Current.CancellationToken));
        Assert.False(await access.CanViewAsync(stranger, new RecordRef(OrderConstants.RecordKind, "order1"), TimelineVisibility.Internal, TestContext.Current.CancellationToken));
        Assert.True(await access.CanAddAsync(manager, new RecordRef(OrderConstants.RecordKind, "order1"), TimelineConstants.Kinds.Note, TimelineVisibility.Internal, TestContext.Current.CancellationToken));
        Assert.False(await access.CanAddAsync(manager, new RecordRef(OrderConstants.RecordKind, "made-up"), TimelineConstants.Kinds.Note, TimelineVisibility.Internal, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AMention_TellsAColleagueWhoMayReadIt_WithALinkAndAnEncodedTitle_ButNotTheText()
    {
        // Arrange: maria may read the entry, bob may not, and the author mentions herself too.
        var sent = new List<(string User, NotificationMessageCapture Message)>();
        var handler = MentionHandler(sent, readers: ["maria"]);
        var entry = new TimelineEntry
        {
            Subject = new RecordRef("Lead", "l1"),
            Kind = TimelineConstants.Kinds.Note,
            Visibility = TimelineVisibility.Internal,
            Author = new PartyRef(PartyKinds.User, "author-id"),
            Body = "Card 4111 1111 for @maria and @bob, cc @author",
        };

        // Act
        await handler.AddedAsync(entry, TestContext.Current.CancellationToken);

        // Assert
        var (user, message) = Assert.Single(sent);
        Assert.Equal("maria", user);
        Assert.DoesNotContain("4111", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("4111", message.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;a href=x&gt;Reset&lt;/a&gt;", message.Summary, StringComparison.Ordinal);
        Assert.Contains("/Admin/leads/l1", message.Html, StringComparison.Ordinal);
        Assert.Equal(TimelineConstants.MentionNotificationType, message.Type);
        Assert.Equal("Lead:l1", message.ReferenceId);
        Assert.Equal("/Admin/leads/l1", message.Url);
    }

    private static TimelineMentionHandler MentionHandler(List<(string User, NotificationMessageCapture Message)> sent, string[] readers)
    {
        var users = new Dictionary<string, IUser>(StringComparer.OrdinalIgnoreCase)
        {
            ["maria"] = new User { UserName = "maria" },
            ["bob"] = new User { UserName = "bob" },
            ["author"] = new User { UserName = "author" },
        };

        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((string name) => users.GetValueOrDefault(name));
        userManager.Setup(manager => manager.GetUserIdAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => user.UserName == "author" ? "author-id" : user.UserName + "-id");

        var principals = new Mock<IUserClaimsPrincipalFactory<IUser>>();
        principals.Setup(factory => factory.CreateAsync(It.IsAny<IUser>()))
            .ReturnsAsync((IUser user) => new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user.UserName)], "test")));

        var access = new Mock<ITimelineAccessService>();
        access.Setup(service => service.CanViewAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<RecordRef>(), It.IsAny<TimelineVisibility>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal principal, RecordRef _, TimelineVisibility _, CancellationToken _) => readers.Contains(principal.Identity.Name));

        var records = new Mock<IRecordDirectory>();
        records.Setup(directory => directory.GetSummaryAsync(It.IsAny<RecordRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecordSummary { Id = "l1", Title = "<a href=x>Reset</a>", AdminUrl = "/Admin/leads/l1" });

        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object user, INotificationMessage message, CancellationToken _) => sent.Add((((User)user).UserName, new NotificationMessageCapture(message.Summary, message.TextBody, message.HtmlBody, message.Type, message.ReferenceId, message.Url))))
            .ReturnsAsync(new NotificationSendResult());

        return new TimelineMentionHandler(userManager.Object, principals.Object, access.Object, records.Object, notifications.Object, new PassThrough());
    }

    private static IAuthorizationService Authorization()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => user.HasClaim(global::OrchardCore.Security.Permissions.Permission.ClaimType, requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorization.Object;
    }

    private static ClaimsPrincipal Manager(params string[] permissions)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "manager"), .. permissions.Select(permission => new Claim(global::OrchardCore.Security.Permissions.Permission.ClaimType, permission))],
            "test"));

    private sealed record NotificationMessageCapture(string Summary, string Text, string Html, string Type, string ReferenceId, string Url);

    private sealed class User : IUser
    {
        public string UserName { get; set; }
    }

    private sealed class PassThrough : IStringLocalizer<TimelineMentionHandler>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
