using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public static class AIDeploymentManagerExtensions
{
    public static async ValueTask<AIDeployment> ResolveUtilityOrDefaultAsync(
        this IAIDeploymentManager deploymentManager,
        string utilityDeploymentName = null,
        string chatDeploymentName = null,
        string clientName = null,
        string connectionName = null)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);

        return await deploymentManager.ResolveOrDefaultAsync(
            AIDeploymentType.Utility,
            utilityDeploymentName,
            clientName,
            connectionName)
            ?? await deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentType.Chat,
                chatDeploymentName,
                clientName,
                connectionName);
    }

    public static async ValueTask<AIDeployment> ResolveAsync(
        this IAIDeploymentManager deploymentManager,
        AIDeploymentType type,
        string deploymentName = null,
        string clientName = null,
        string connectionName = null)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);

        var deployment = await deploymentManager.ResolveOrDefaultAsync(type, deploymentName, clientName, connectionName);

        return deployment ?? throw new InvalidOperationException($"Unable to resolve an AI deployment for type '{type}' with deploymentName '{deploymentName ?? "(null)"}', clientName '{clientName ?? "(null)"}', and connectionName '{connectionName ?? "(null)"}'.");
    }

    public static async ValueTask<AIDeployment> ResolveUtilityAsync(
        this IAIDeploymentManager deploymentManager,
        string utilityDeploymentName = null,
        string chatDeploymentName = null,
        string clientName = null,
        string connectionName = null)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);

        var deployment = await deploymentManager.ResolveUtilityOrDefaultAsync(utilityDeploymentName, chatDeploymentName, clientName, connectionName);

        return deployment ?? throw new InvalidOperationException($"Unable to resolve a utility AI deployment using utilityDeploymentName '{utilityDeploymentName ?? "(null)"}', chatDeploymentName '{chatDeploymentName ?? "(null)"}', clientName '{clientName ?? "(null)"}', and connectionName '{connectionName ?? "(null)"}'.");
    }
}
