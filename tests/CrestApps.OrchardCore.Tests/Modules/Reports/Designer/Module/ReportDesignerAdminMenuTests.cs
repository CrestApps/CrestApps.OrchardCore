using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Navigation;
using OrchardCore.Security;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

/// <summary>
/// A report pinned to the admin menu is listed under the (nested) item of its group, or directly under Reports, and a
/// group shows only when it leads to a pinned report the user may run.
/// </summary>
public sealed class ReportDesignerAdminMenuTests
{
    private static readonly ClaimsPrincipal _designer = User("designer", "Designer");

    [Fact]
    public async Task PinnedReports_AreListedUnderTheirNestedGroups_OrDirectlyUnderReports()
    {
        // Arrange
        var groups = Catalog(
            new ReportGroup { ItemId = "sales", DisplayText = "Sales", Order = 1 },
            new ReportGroup { ItemId = "regional", DisplayText = "Regional", ParentId = "sales" },
            new ReportGroup { ItemId = "billing", DisplayText = "Billing" },
            new ReportGroup { ItemId = "empty", DisplayText = "Empty" },
            new ReportGroup { ItemId = "private", DisplayText = "Private" });
        var designs = Catalog(
            Report("top", "Top customers", null),
            Report("revenue", "Regional revenue", "regional"),
            Report("invoices", "Invoices", "billing"),
            Report("unpinned", "Not pinned", "sales", pinned: false),
            Report("hidden", "Hidden", "private"),
            Report("orphan", "Orphan", "deleted"));

        // Act
        var menu = await BuildAsync(designs, groups);

        // Assert
        var reports = Assert.Single(menu, item => item.Text.Value == "Reports");
        Assert.Equal(["Report Builder", "Report Views", "Report Groups", "Billing", "Sales", "Orphan", "Top customers"], Texts(reports));

        var sales = Child(reports, "Sales");
        Assert.Equal(["Regional"], Texts(sales));
        Assert.Equal(["Regional revenue"], Texts(Child(sales, "Regional")));
        Assert.Equal(["Invoices"], Texts(Child(reports, "Billing")));
    }

    [Fact]
    public async Task WithoutPinnedReports_NoGroupIsShown()
    {
        // Arrange
        var groups = Catalog(new ReportGroup { ItemId = "sales", DisplayText = "Sales" });
        var designs = Catalog(Report("unpinned", "Not pinned", "sales", pinned: false));

        // Act
        var menu = await BuildAsync(designs, groups);

        // Assert
        var reports = Assert.Single(menu, item => item.Text.Value == "Reports");
        Assert.Equal(["Report Builder", "Report Views", "Report Groups"], Texts(reports));
    }

    private static ReportDesign Report(string id, string title, string groupId, bool pinned = true)
    {
        return new ReportDesign
        {
            ItemId = id,
            DisplayText = title,
            GroupId = groupId,
            ShowInAdminMenu = pinned,
            OwnerId = "owner",
        };
    }

    private static string[] Texts(MenuItem item)
    {
        return [.. item.Items.Select(child => child.Text.Value)];
    }

    private static MenuItem Child(MenuItem item, string text)
    {
        return Assert.Single(item.Items, child => child.Text.Value == text);
    }

    private static async Task<IEnumerable<MenuItem>> BuildAsync(
        CrestApps.Core.Services.ICatalog<ReportDesign> designs,
        CrestApps.Core.Services.ICatalog<ReportGroup> groups)
    {
        // The designer may design and manage groups, and run every report but the hidden one.
        var authorizationService = new FakeAuthorizationService(
            ReportDesignerPermissions.ManageOwnReportDesigns.Name,
            ReportDesignerPermissions.ManageReportGroups.Name)
            .With(new EveryReportBut("hidden"));
        var httpContext = new DefaultHttpContext { User = _designer };
        var provider = new ReportDesignerAdminMenu(
            designs,
            groups,
            new HttpContextAccessor { HttpContext = httpContext },
            authorizationService,
            new PassThroughStringLocalizer<ReportDesignerAdminMenu>());

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(helper => helper.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("/admin/report");
        urlHelper.Setup(helper => helper.Content(It.IsAny<string>())).Returns((string path) => path);
        var urlHelperFactory = new Mock<IUrlHelperFactory>();
        urlHelperFactory.Setup(factory => factory.GetUrlHelper(It.IsAny<ActionContext>())).Returns(urlHelper.Object);

        var navigationManager = new NavigationManager(
            [provider],
            NullLogger<NavigationManager>.Instance,
            urlHelperFactory.Object,
            authorizationService);

        return await navigationManager.BuildMenuAsync(NavigationConstants.AdminId, new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));
    }

    private sealed class EveryReportBut : IAuthorizationHandler
    {
        private readonly string _hiddenId;

        public EveryReportBut(string hiddenId)
        {
            _hiddenId = hiddenId;
        }

        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            if (context.Resource is ReportDesign design && design.ItemId != _hiddenId)
            {
                foreach (var requirement in context.PendingRequirements.OfType<PermissionRequirement>()
                    .Where(requirement => requirement.Permission.Name == ReportDesignerPermissions.ViewAllReportDesigns.Name)
                    .ToList())
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
