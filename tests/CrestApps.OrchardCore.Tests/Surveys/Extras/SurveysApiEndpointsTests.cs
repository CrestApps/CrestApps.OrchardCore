using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

public sealed class SurveysApiEndpointsTests
{
    [Fact]
    public async Task ListSurveys_WithoutManagePermission_IsForbidden()
    {
        // Arrange
        var http = Context();

        // Act
        var result = await SurveysApiEndpoints.ListSurveysAsync(http, Deny(), null, null, null);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task Definition_WithoutRespondPermission_IsForbidden()
    {
        // Arrange
        var http = Context();

        // Act
        var result = await SurveysApiEndpoints.GetDefinitionAsync("s1", null, http, Deny(), null, null, null, null);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task Start_WithoutRespondPermission_IsForbidden()
    {
        // Arrange
        var http = Context();

        // Act
        var result = await SurveysApiEndpoints.StartAsync(new StartResponseInput { SurveyId = "s1" }, http, Deny(), null);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task Complete_WithoutRespondPermission_IsForbidden()
    {
        // Arrange
        var http = Context();

        // Act
        var result = await SurveysApiEndpoints.CompleteAsync("r1", new SubmitPageInput(), http, Deny(), null);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    private static DefaultHttpContext Context()
        => new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")], "Api")) };

    private static IAuthorizationService Deny()
    {
        var service = new Mock<IAuthorizationService>();

        service.Setup(authorization => authorization.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        return service.Object;
    }
}
