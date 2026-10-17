using System.Security.Claims;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Tests.Modules.Reports.Contents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.Reports;

/// <summary>
/// Other modules add their own items under the admin Reports menu. When menus merge, the item with the higher priority
/// keeps its id and classes, which is what gives the Reports menu its icon.
/// </summary>
public sealed class ReportsAdminMenuTests
{
    [Fact]
    public async Task ReportsMenu_KeepsItsIdAndClass_WhenAnotherModuleAddsToIt()
    {
        // Arrange
        var report = new Mock<IReport>();
        report.SetupGet(item => item.Name).Returns("Sales");
        report.SetupGet(item => item.DisplayName).Returns(new LocalizedString("Sales", "Sales"));
        report.SetupGet(item => item.Permission).Returns(new Permission("ViewSalesReport"));
        var reportManager = new Mock<IReportManager>();
        reportManager.Setup(manager => manager.GetReports()).Returns([report.Object]);

        // The other module's item is added first, so equal priorities would keep its (missing) id.
        var navigationManager = Manager(
            new OtherModuleMenu(),
            new ReportsAdminMenu(reportManager.Object, new ContentReportTestLocalizer<ReportsAdminMenu>()));

        // Act
        var menu = await navigationManager.BuildMenuAsync(NavigationConstants.AdminId, ActionContext());

        // Assert
        var reports = Assert.Single(menu, item => item.Text.Value == "Reports");
        Assert.Equal("reports", reports.Id);
        Assert.Contains("reports", reports.Classes);
        Assert.Contains(reports.Items, item => item.Text.Value == "Other report");
    }

    private static NavigationManager Manager(params INavigationProvider[] providers)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(helper => helper.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("/admin/report");
        urlHelper.Setup(helper => helper.Content(It.IsAny<string>())).Returns((string path) => path);
        var urlHelperFactory = new Mock<IUrlHelperFactory>();
        urlHelperFactory.Setup(factory => factory.GetUrlHelper(It.IsAny<ActionContext>())).Returns(urlHelper.Object);

        return new NavigationManager(
            providers,
            NullLogger<NavigationManager>.Instance,
            new ShellSettings(),
            urlHelperFactory.Object,
            authorizationService.Object);
    }

    private static ActionContext ActionContext()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "Test")),
        };

        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    }

    private sealed class OtherModuleMenu : AdminNavigationProvider
    {
        protected override ValueTask BuildAsync(NavigationBuilder builder)
        {
            builder.Add(new LocalizedString("Reports", "Reports"), "after.40", reports => reports
                .Add(new LocalizedString("Other report", "Other report"), "9", item => item
                    .Url("/admin/other-report")
                    .LocalNav()));

            return ValueTask.CompletedTask;
        }
    }
}
