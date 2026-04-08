using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.Core.AI.Services;

public static class EmbeddingDeploymentResolver
{
    public static async Task<AIDeployment> FindEmbeddingDeploymentAsync(
        IAIDeploymentManager deploymentManager,
        DataSourceIndexProfileMetadata metadata,
        string deploymentIdOrName = null)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);

        var selector = string.IsNullOrWhiteSpace(deploymentIdOrName)
            ? metadata?.EmbeddingDeploymentId
            : deploymentIdOrName;

        if (!string.IsNullOrWhiteSpace(selector))
        {
            var deployment = await deploymentManager.FindByIdAsync(selector) ??
                await deploymentManager.FindByNameAsync(selector);

            if (deployment != null)
            {
                return deployment;
            }
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (metadata == null ||
            string.IsNullOrWhiteSpace(metadata.EmbeddingProviderName) ||
            string.IsNullOrWhiteSpace(metadata.EmbeddingConnectionName) ||
            string.IsNullOrWhiteSpace(metadata.EmbeddingDeploymentName))
        {
            return null;
        }

        var legacyDeployment = await deploymentManager.FindByNameAsync(metadata.EmbeddingDeploymentName);

        if (IsMatchingDeployment(legacyDeployment, metadata))
        {
            return legacyDeployment;
        }

        var deployments = await deploymentManager.GetAllAsync(metadata.EmbeddingProviderName, metadata.EmbeddingConnectionName);

        return deployments.FirstOrDefault(deployment =>
            deployment.SupportsType(AIDeploymentType.Embedding) &&
            (string.Equals(deployment.Name, metadata.EmbeddingDeploymentName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(deployment.ModelName, metadata.EmbeddingDeploymentName, StringComparison.OrdinalIgnoreCase)));
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public static async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        DataSourceIndexProfileMetadata metadata,
        string deploymentIdOrName = null)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);
        ArgumentNullException.ThrowIfNull(aiClientFactory);

        var deployment = await FindEmbeddingDeploymentAsync(deploymentManager, metadata, deploymentIdOrName);

        if (deployment != null &&
            !string.IsNullOrWhiteSpace(deployment.ClientName) &&
            !string.IsNullOrWhiteSpace(deployment.ConnectionName) &&
            !string.IsNullOrWhiteSpace(deployment.ModelName))
        {
            return await aiClientFactory.CreateEmbeddingGeneratorAsync(
                deployment.ClientName,
                deployment.ConnectionName,
                deployment.ModelName);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (metadata == null ||
            string.IsNullOrWhiteSpace(metadata.EmbeddingProviderName) ||
            string.IsNullOrWhiteSpace(metadata.EmbeddingConnectionName) ||
            string.IsNullOrWhiteSpace(metadata.EmbeddingDeploymentName))
        {
            return null;
        }

        return await aiClientFactory.CreateEmbeddingGeneratorAsync(
            metadata.EmbeddingProviderName,
            metadata.EmbeddingConnectionName,
            metadata.EmbeddingDeploymentName);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    private static bool IsMatchingDeployment(AIDeployment deployment, DataSourceIndexProfileMetadata metadata)
    {
        if (deployment == null || !deployment.SupportsType(AIDeploymentType.Embedding))
        {
            return false;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        return string.Equals(deployment.ClientName, metadata.EmbeddingProviderName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(deployment.ConnectionName, metadata.EmbeddingConnectionName, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(deployment.Name, metadata.EmbeddingDeploymentName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(deployment.ModelName, metadata.EmbeddingDeploymentName, StringComparison.OrdinalIgnoreCase));
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
