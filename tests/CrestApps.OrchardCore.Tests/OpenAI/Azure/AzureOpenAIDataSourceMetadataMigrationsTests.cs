using System.Reflection;

using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Azure;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using Moq;

#pragma warning disable CS0618 // Type or member is obsolete

namespace CrestApps.OrchardCore.Tests.OpenAI.Azure;

public sealed class AzureOpenAIDataSourceMetadataMigrationsTests
{
    [Fact]

    public async Task FindFirstEmbeddingMetadata_WhenEmbeddingConnectionExists_ShouldReturnMetadata()
    {
        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(x => x.GetByTypeAsync(AIDeploymentType.Embedding))
            .Returns(new ValueTask<IEnumerable<AIDeployment>>(
            [
                new AIDeployment { ItemId = "embedding-1", Name = "embedding-1" },
            ]));

        var serviceProvider = new ServiceCollection()
            .AddSingleton(deploymentManager.Object)
            .BuildServiceProvider();
        var logger = Mock.Of<ILogger>();
        var metadata = await InvokeFindFirstEmbeddingMetadata(serviceProvider, logger);

        Assert.Equal("embedding-1", metadata.GetEmbeddingDeploymentName());
    }

    [Fact]

    public async Task FindFirstEmbeddingMetadata_WhenNoEmbeddingConnectionConfigured_ShouldLogWarningAndReturnEmptyMetadata()
    {
        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(x => x.GetByTypeAsync(AIDeploymentType.Embedding))
            .Returns(new ValueTask<IEnumerable<AIDeployment>>(Array.Empty<AIDeployment>()));

        var serviceProvider = new ServiceCollection()
            .AddSingleton(deploymentManager.Object)
            .BuildServiceProvider();
        var logger = new Mock<ILogger>();
        var metadata = await InvokeFindFirstEmbeddingMetadata(serviceProvider, logger.Object);

        Assert.True(string.IsNullOrEmpty(metadata.GetEmbeddingDeploymentName()));

        logger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No embedding deployment was found")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    private static async Task<DataSourceIndexProfileMetadata> InvokeFindFirstEmbeddingMetadata(IServiceProvider serviceProvider, ILogger logger)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.OpenAI.Azure.Migrations.AzureOpenAIDataSourceMetadataMigrations", throwOnError: true)!
            .GetMethod("FindFirstEmbeddingMetadataAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

        var task = (Task<DataSourceIndexProfileMetadata>)method.Invoke(null, [serviceProvider, logger])!;

        return await task;
    }
}
