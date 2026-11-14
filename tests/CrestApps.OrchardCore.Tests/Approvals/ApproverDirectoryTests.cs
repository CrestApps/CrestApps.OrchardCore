using System.Security.Claims;
using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Approvals;

/// <summary>
/// Who each approver rule names, and the notices approvers and requesters get in the notification center.
/// </summary>
public sealed class ApproverDirectoryTests
{
    private static readonly Permission _finance = new("ApproveFinance", global::OrchardCore.Localization.LocalizationSource.Create<ApproverDirectoryTests>("Approve finance"));

    [Fact]
    public async Task PermissionRule_NamesThoseTheAuthorizationServiceGrants()
    {
        // Arrange
        var directory = Directory(new ServiceCollection(), TestSecurity.Granting(_finance));
        var rule = new ApproverRule { Kind = ApproverKind.Permission, Value = "ApproveFinance" };
        var unknown = new ApproverRule { Kind = ApproverKind.Permission, Value = "Missing" };

        // Act + Assert
        Assert.True(await directory.IsNamedAsync(rule, new ApprovalRequest(), TestSecurity.User("pat"), TestContext.Current.CancellationToken));
        Assert.False(await directory.IsNamedAsync(unknown, new ApprovalRequest(), TestSecurity.User("pat"), TestContext.Current.CancellationToken));
        Assert.False(await directory.IsNamedAsync(rule, new ApprovalRequest(), new ClaimsPrincipal(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RoleRule_NamesMembersOfTheRole()
    {
        // Arrange
        var directory = Directory(new ServiceCollection(), TestSecurity.Granting());
        var rule = new ApproverRule { Kind = ApproverKind.Role, Value = "Payroll" };
        var member = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "pat"), new Claim(ClaimTypes.NameIdentifier, "pat-id"), new Claim(ClaimTypes.Role, "Payroll")], "test"));

        // Act + Assert
        Assert.True(await directory.IsNamedAsync(rule, new ApprovalRequest(), member, TestContext.Current.CancellationToken));
        Assert.False(await directory.IsNamedAsync(rule, new ApprovalRequest(), TestSecurity.User("lee"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ManagerRule_NamesTheManagersUpTheChain_OfTheStaffMemberTheRequestIsAbout()
    {
        // Arrange
        var hierarchy = new Mock<IStaffHierarchy>();
        hierarchy.Setup(service => service.GetManagerChainAsync("staff-kim", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StaffMember { ItemId = "staff-pat", UserId = "pat-id" }, new StaffMember { ItemId = "staff-lee", UserId = "lee-id" }, new StaffMember { ItemId = "staff-none" }]);
        var services = new ServiceCollection().AddSingleton(hierarchy.Object);
        var directory = Directory(services, TestSecurity.Granting());
        var rule = new ApproverRule { Kind = ApproverKind.Manager };
        var request = new ApprovalRequest { SubjectStaffMemberId = "staff-kim" };

        // Act
        var named = await directory.IsNamedAsync(rule, request, TestSecurity.User("lee"), TestContext.Current.CancellationToken);
        var stranger = await directory.IsNamedAsync(rule, request, TestSecurity.User("sam"), TestContext.Current.CancellationToken);
        var ids = await directory.GetUserIdsAsync(rule, request, TestContext.Current.CancellationToken);
        var noSubject = await directory.GetUserIdsAsync(rule, new ApprovalRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(named);
        Assert.False(stranger);
        Assert.Equal(["pat-id", "lee-id"], ids);
        Assert.Empty(noSubject);
    }

    [Fact]
    public async Task StaffRule_NamesTheUsersOfTheListedStaffMembers()
    {
        // Arrange
        var staff = new Mock<IStaffDirectory>();
        staff.Setup(service => service.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids.ToDictionary(id => id, id => new StaffMember { ItemId = id, UserId = id.Replace("staff-", string.Empty, StringComparison.Ordinal) + "-id" }));
        var directory = Directory(new ServiceCollection().AddSingleton(staff.Object), TestSecurity.Granting());
        var rule = new ApproverRule { Kind = ApproverKind.Staff, Value = "staff-pat, staff-lee" };

        // Act + Assert
        Assert.True(await directory.IsNamedAsync(rule, new ApprovalRequest(), TestSecurity.User("lee"), TestContext.Current.CancellationToken));
        Assert.False(await directory.IsNamedAsync(rule, new ApprovalRequest(), TestSecurity.User("kim"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NotificationObserver_AsksTheApprovers_ThenRemovesTheNotices_AndTellsTheRequester()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy();
        var sent = new List<(string UserId, NotificationMessage Message)>();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object user, INotificationMessage message, CancellationToken _) => sent.Add((((User)user).UserId, (NotificationMessage)message)))
            .ReturnsAsync(new NotificationSendResult());
        var manager = new Mock<INotificationManager>();
        var users = new Mock<IUserStore<IUser>>();
        users.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => new User { UserId = id, UserName = id.Replace("-id", string.Empty, StringComparison.Ordinal) });
        var services = new ServiceCollection()
            .AddSingleton(notifications.Object)
            .AddSingleton(manager.Object)
            .AddSingleton(new UserManager<IUser>(users.Object, null, null, null, null, null, null, null, null))
            .AddSingleton<CrestApps.OrchardCore.Approvals.Services.IApprovalService>(context.Service)
            .BuildServiceProvider();
        context.Observers.Add(new ApprovalNotificationObserver(services, RecipeRoundTrip.Localizer<ApprovalNotificationObserver>()));

        // Act
        var request = await context.RequestAsync(requester: "riley");
        var asked = sent.ToList();
        sent.Clear();
        await context.Service.DecideAsync(request, TestSecurity.User("pat"), ApprovalDecisionKind.Reject, "Missing hours", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["kim-id", "lee-id", "pat-id"], asked.Select(entry => entry.UserId));
        Assert.All(asked, entry =>
        {
            Assert.Equal(ApprovalsConstants.ApprovalNotificationType, entry.Message.Type);
            Assert.Equal(request.ItemId, entry.Message.ReferenceId);
            Assert.Equal("~/Admin/approvals/decide/" + request.ItemId, entry.Message.Url);
        });
        manager.Verify(service => service.DeleteAsync(It.Is<NotificationFilter>(filter => filter.Type == ApprovalsConstants.ApprovalNotificationType && filter.ReferenceId == request.ItemId), It.IsAny<CancellationToken>()), Times.Once);
        var told = Assert.Single(sent);
        Assert.Equal("riley-id", told.UserId);
        Assert.Equal(ApprovalsConstants.DecisionNotificationType, told.Message.Type);
        Assert.Contains("Missing hours", told.Message.Summary, StringComparison.Ordinal);
    }

    private static ApproverDirectory Directory(IServiceCollection services, Microsoft.AspNetCore.Authorization.IAuthorizationService authorization)
    {
        var provider = new Mock<IPermissionProvider>();
        provider.Setup(entry => entry.GetPermissionsAsync()).ReturnsAsync([_finance]);

        return new ApproverDirectory(authorization, [provider.Object], services.BuildServiceProvider());
    }
}
