using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Drivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Security;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// My courses in the user menu is for people who may use Learning; the courses come from one capped query only then.
/// </summary>
public sealed class LearningUserMenuTests
{
    [Fact]
    public async Task MyCourses_IsLeftOutOfTheUserMenu_ForAUserWithoutAccessLearning()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")], "Test"));
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(user, It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(value => value.HttpContext).Returns(new DefaultHttpContext { User = user });

        // No store and no content manager: nothing is read for a user who doesn't see the entry.
        var driver = new LearningUserMenuDisplayDriver(authorization.Object, accessor.Object, null, Mock.Of<IContentManager>(MockBehavior.Strict));

        // Act
        var result = await driver.DisplayAsync(new UserMenu(), null);

        // Assert
        Assert.Null(result);
        authorization.Verify(service => service.AuthorizeAsync(user, It.IsAny<object>(), It.Is<IEnumerable<IAuthorizationRequirement>>(requirements => requirements.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == LearningPermissions.AccessLearning.Name))), Times.Once);
    }

    [Fact]
    public async Task MyCourses_IsLeftOutOfTheUserMenu_ForAnonymousVisitors()
    {
        // Arrange
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(value => value.HttpContext).Returns(new DefaultHttpContext());
        var driver = new LearningUserMenuDisplayDriver(Mock.Of<IAuthorizationService>(MockBehavior.Strict), accessor.Object, null, Mock.Of<IContentManager>(MockBehavior.Strict));

        // Act
        var result = await driver.DisplayAsync(new UserMenu(), null);

        // Assert
        Assert.Null(result);
    }
}
