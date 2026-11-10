using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Deployments;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// Agent platforms travel between sites through a deployment plan, without the API key or the signing secret: the
/// importing site gives the platform new ones.
/// </summary>
public sealed class AgentPlatformsDeploymentSourceTests
{
    [Fact]
    public async Task Deployment_ExportsThePlatform_WithoutItsCredentials()
    {
        // Arrange
        var manager = new Mock<IAgentPlatformManager>();
        manager.Setup(entry => entry.GetAllAsync()).ReturnsAsync(
        [
            new AgentPlatform
            {
                ItemId = "p1",
                Name = "Shopping assistant",
                TestMode = true,
                Protocols = ["ucp"],
                ApiKeyHash = "hash",
                ApiKeyHint = "agk_...abcd",
                ProtectedWebhookSecret = "protected",
                WebhookUrl = "https://agent.example.com/orders",
            },
        ]);
        var source = new AgentPlatformsDeploymentSource(manager.Object);
        var result = new DeploymentPlanResult(null, new RecipeDescriptor());

        // Act
        await source.ProcessDeploymentStepAsync(new AgentPlatformsDeploymentStep(), result);

        // Assert
        var step = Assert.Single(result.Steps);
        var entry = Assert.Single(step["Platforms"]!.AsArray())!.AsObject();

        Assert.Equal("Shopping assistant", entry["Name"]!.GetValue<string>());
        Assert.Equal("https://agent.example.com/orders", entry["WebhookUrl"]!.GetValue<string>());
        Assert.DoesNotContain(entry, property => property.Key.Equals(nameof(AgentPlatform.ApiKeyHash), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entry, property => property.Key.Equals(nameof(AgentPlatform.ApiKeyHint), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entry, property => property.Key.Equals(nameof(AgentPlatform.ProtectedWebhookSecret), StringComparison.OrdinalIgnoreCase));
    }
}
