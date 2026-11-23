using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Api;
using CrestApps.OrchardCore.Surveys.Core.Access;
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
        var result = await SurveysApiEndpoints.ListSurveysAsync(null, null, http, Deny(), null);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task Definition_WithoutRespondPermission_IsForbidden()
    {
        // Arrange
        var http = Context();

        // Act
        var result = await SurveysApiEndpoints.GetDefinitionAsync("s1", null, null, http, Deny(), null, null, null, null, null, null, null, null, null);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task Start_WithoutRespondPermission_IsForbidden()
    {
        // Arrange
        var http = Context();

        // Act
        var result = await SurveysApiEndpoints.StartAsync(new StartResponseInput { SurveyId = "s1" }, http, Deny(), null, null, null, null);

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

    [Fact]
    public async Task EveryCall_SendsNoStore()
    {
        // Arrange: the respondent GETs return answers, so no browser or shared proxy may keep them.
        var http = Context();
        var invocation = new DefaultEndpointFilterInvocationContext(http);

        // Act
        var result = await SurveysApiEndpoints.NoStoreAndUnreadableSecretFilterAsync(invocation, _ => ValueTask.FromResult<object>(TypedResults.Ok()));

        // Assert
        Assert.IsType<Ok>(result);
        Assert.Equal("no-store", http.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task UnreadableAnonymitySecret_Answers503ProblemInsteadOfAnUnhandledError()
    {
        // Arrange: minimal APIs skip MVC exception filters, so the group's endpoint filter turns the failure into a result.
        var http = Context();
        var invocation = new DefaultEndpointFilterInvocationContext(http);

        // Act
        var result = await SurveysApiEndpoints.NoStoreAndUnreadableSecretFilterAsync(invocation, _ => throw new SurveyAnonymitySecretUnreadableException("unreadable"));

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
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
