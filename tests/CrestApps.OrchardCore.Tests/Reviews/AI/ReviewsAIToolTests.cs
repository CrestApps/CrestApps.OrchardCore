using CrestApps.OrchardCore.Reviews;
using CrestApps.OrchardCore.Reviews.AI;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Reviews.AI;

/// <summary>
/// The review tools need Moderate product reviews (code name <c>ManageProductReviews</c>) and check their arguments
/// before they read a review. <c>ReviewService</c> reads reviews through YesSql index queries, so its list, moderate and
/// delete paths are not run here.
/// </summary>
public sealed class ReviewsAIToolTests
{
    public static TheoryData<string> ToolNames => new()
    {
        ListProductReviewsTool.TheName,
        ModerateProductReviewTool.TheName,
        DeleteProductReviewTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(ReviewsAIToolBase.NotAllowedMessage, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutModerateProductReviews(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != ReviewsPermissions.ManageReviews.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(ReviewsAIToolBase.NotAllowedMessage, result);
        Assert.Equal("ManageProductReviews", Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
    }

    [Fact]
    public async Task ListProductReviews_RefusesAnUnknownStatus()
    {
        // Act
        var result = await InvokeAsync(ListProductReviewsTool.TheName, Build(), new() { ["status"] = "flagged" });

        // Assert
        Assert.StartsWith("Nothing was listed: status must be", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModerateProductReview_RefusesAnUnknownAction()
    {
        // Act
        var result = await InvokeAsync(ModerateProductReviewTool.TheName, Build(), new() { ["reviewId"] = "review-1", ["action"] = "hide" });

        // Assert
        Assert.Equal("Nothing was changed: action must be approve or reject.", result);
    }

    [Fact]
    public async Task Tool_RefusesAnArgumentItDoesNotDeclare()
    {
        // Act
        var result = await InvokeAsync(DeleteProductReviewTool.TheName, Build(), new() { ["reviewId"] = "review-1", ["force"] = true });

        // Assert
        Assert.Contains("'force' is not an argument of deleteProductReview", result, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            ModerateProductReviewTool.TheName => new() { ["reviewId"] = "review-1", ["action"] = "approve" },
            DeleteProductReviewTool.TheName => new() { ["reviewId"] = "review-1" },
            _ => [],
        };

    private static ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
        => AgentToolTestContext.CreateServices(signedIn, authorize).BuildServiceProvider();

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListProductReviewsTool.TheName => new ListProductReviewsTool(),
            ModerateProductReviewTool.TheName => new ModerateProductReviewTool(),
            _ => new DeleteProductReviewTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
