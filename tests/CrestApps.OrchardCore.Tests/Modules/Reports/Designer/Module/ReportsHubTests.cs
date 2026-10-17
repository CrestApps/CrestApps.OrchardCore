using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.RealTime;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Security;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

/// <summary>
/// The reports hub only lets people who may edit a report follow it, keeps tenants apart, and relays presence.
/// </summary>
public sealed class ReportsHubTests
{
    private const string Tenant = "Default";

    [Fact]
    public async Task Subscribe_ToAReportTheUserMayEdit_JoinsItsTenantGroup_AndTellsTheOthers()
    {
        // Arrange
        var (hub, groups, others) = Hub(User("ada", "Ada"), canEdit: true);

        // Act
        var subscribed = await hub.Subscribe("r1");

        // Assert
        Assert.True(subscribed);
        var group = TenantSignalRGroupName.ForGroup(Tenant, "report-design:r1");
        groups.Verify(value => value.AddToGroupAsync("c1", group, HubConnectionWork.MustComplete), Times.Once());
        others.Verify(value => value.SendCoreAsync("PresenceJoined", It.Is<object[]>(args => ((ReportPresence)args[0]).UserName == "Ada"), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Subscribe_ToAReportTheUserMayNotEdit_IsRefused()
    {
        // Arrange
        var (hub, groups, _) = Hub(User("bob", "Bob"), canEdit: false);

        // Act
        var subscribed = await hub.Subscribe("r1");
        var missing = await hub.Subscribe("missing");

        // Assert
        Assert.False(subscribed);
        Assert.False(missing);
        groups.Verify(value => value.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task AnnouncePresence_OnlyForAReportThisConnectionFollows()
    {
        // Arrange
        var (hub, _, _) = Hub(User("ada", "Ada"), canEdit: true, out var clients);
        var client = new Mock<ISingleClientProxy>();
        clients.Setup(value => value.Client("c2")).Returns(client.Object);

        // Act
        await hub.AnnouncePresence("r1", "c2");
        await hub.Subscribe("r1");
        await hub.AnnouncePresence("r1", "c2");

        // Assert
        client.Verify(value => value.SendCoreAsync("PresenceHere", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Notifier_SendsTheChangeToTheReportsTenantGroup()
    {
        // Arrange
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForGroup(Tenant, "report-design:r1"))).Returns(proxy.Object);
        var hub = new Mock<IHubContext<ReportsHub>>();
        hub.SetupGet(value => value.Clients).Returns(clients.Object);

        // Act
        await SignalRReportDesignNotifier.SendAsync(hub.Object, Tenant, new ReportDesignChange
        {
            Kind = ReportDesignChangeKind.Published,
            DesignId = "r1",
            Revision = 4,
            VersionNumber = 2,
            UserName = "Ada",
        });

        // Assert
        proxy.Verify(value => value.SendCoreAsync(SignalRReportDesignNotifier.ClientMethod, It.Is<object[]>(args => args.Length == 1), It.IsAny<CancellationToken>()), Times.Once());
    }

    private static (ReportsHub Hub, Mock<IGroupManager> Groups, Mock<IClientProxy> Others) Hub(ClaimsPrincipal user, bool canEdit)
    {
        return Hub(user, canEdit, out _);
    }

    private static (ReportsHub Hub, Mock<IGroupManager> Groups, Mock<IClientProxy> Others) Hub(ClaimsPrincipal user, bool canEdit, out Mock<IHubCallerClients> clients)
    {
        var clock = new Mock<IClock>();
        var designService = new ReportDesignService(
            Catalog(new ReportDesign { ItemId = "r1", DisplayText = "Sales", OwnerId = "owner" }),
            Catalog<ReportView>(),
            new ReportShareLinkService(Catalog<ReportShareLink>(), clock.Object),
            NoSnapshots(),
            Planner(SalesData()),
            DocumentBuilder(),
            clock.Object,
            new PassThroughStringLocalizer<ReportDesignService>());

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal principal, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                canEdit &&
                resource is ReportDesign &&
                requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == ReportDesignerPermissions.ManageAllReportDesigns.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var context = new Mock<HubCallerContext>();
        var items = new Dictionary<object, object>();
        context.SetupGet(value => value.ConnectionId).Returns("c1");
        context.SetupGet(value => value.User).Returns(user);
        context.SetupGet(value => value.Items).Returns(items);

        var groups = new Mock<IGroupManager>();
        var others = new Mock<IClientProxy>();
        clients = new Mock<IHubCallerClients>();
        clients.Setup(value => value.OthersInGroup(It.IsAny<string>())).Returns(others.Object);
        clients.Setup(value => value.Group(It.IsAny<string>())).Returns(others.Object);

        var hub = new ReportsHub(designService, authorizationService.Object, new ShellSettings { Name = Tenant })
        {
            Context = context.Object,
            Groups = groups.Object,
            Clients = clients.Object,
        };

        return (hub, groups, others);
    }
}
