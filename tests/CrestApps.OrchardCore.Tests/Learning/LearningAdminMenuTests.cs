using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Navigation;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Course staff (instructors, assistants and graders hold Manage own courses) see the Learning pages they can open in
/// the admin menu, not only Courses.
/// </summary>
public sealed class LearningAdminMenuTests
{
    [Fact]
    public async Task CourseStaff_SeeEnrollments_InTheLearningMenu()
    {
        // Arrange
        var permissions = new[] { LearningPermissions.ManageOwnCourses.Name };
        var provider = new LearningAdminMenu(StaffContext(), new FakeAuthorizationService(permissions), new PassThroughStringLocalizer<LearningAdminMenu>());

        // Act
        var menu = await BuildAsync([provider], permissions);

        // Assert
        var learning = Assert.Single(menu, item => item.Text.Value == "Learning");
        Assert.Equal(["Courses", "Enrollments"], learning.Items.Select(item => item.Text.Value).Order(StringComparer.Ordinal));
    }

    internal static IHttpContextAccessor StaffContext()
        => new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "staff")], "Test")) } };

    internal static async Task<IEnumerable<MenuItem>> BuildAsync(IEnumerable<INavigationProvider> providers, params string[] permissions)
    {
        var authorizationService = new FakeAuthorizationService(permissions);
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "staff")], "Test")) };
        var urlHelper = new Mock<IUrlHelper>();

        urlHelper.Setup(helper => helper.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("/admin/learning");
        urlHelper.Setup(helper => helper.Content(It.IsAny<string>())).Returns((string path) => path);

        var urlHelperFactory = new Mock<IUrlHelperFactory>();

        urlHelperFactory.Setup(factory => factory.GetUrlHelper(It.IsAny<ActionContext>())).Returns(urlHelper.Object);

        var navigationManager = new NavigationManager(
            providers,
            NullLogger<NavigationManager>.Instance,
            urlHelperFactory.Object,
            authorizationService);

        return await navigationManager.BuildMenuAsync(NavigationConstants.AdminId, new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));
    }
}
