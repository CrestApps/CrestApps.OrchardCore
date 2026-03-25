using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public static class AIDeploymentManagerExtensions
{
    public static async ValueTask<AIDeployment> ResolveUtilityOrDefaultAsync(
        this IAIDeploymentManager deploymentManager,
        string utilityDeploymentId = null,
        string chatDeploymentId = null,
        string clientName = null,
        string connectionName = null)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);

        return await deploymentManager.ResolveOrDefaultAsync(
            AIDeploymentType.Utility,
            utilityDeploymentId,
            clientName,
            connectionName)
            ?? await deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentType.Chat,
                chatDeploymentId,
                clientName,
                connectionName);
    }

    public static async ValueTask<AIDeployment> ResolveAsync(
        this IAIDeploymentManager deploymentManager,
        AIDeploymentType type,
        string deploymentId = null,
        string clientName = null,
        string connectionName = null)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);

        var deployment = await deploymentManager.ResolveOrDefaultAsync(type, deploymentId, clientName, connectionName);

        return deployment ?? throw new InvalidOperationException($"Unable to resolve an AI deployment for type '{type}' with deploymentId '{deploymentId ?? "(null)"}', clientName '{clientName ?? "(null)"}', and connectionName '{connectionName ?? "(null)"}'.");
    }

    public static async ValueTask<AIDeployment> ResolveUtilityAsync(
        this IAIDeploymentManager deploymentManager,
        string utilityDeploymentId = null,
        string chatDeploymentId = null,
        string clientName = null,
        string connectionName = null)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);

        var deployment = await deploymentManager.ResolveUtilityOrDefaultAsync(utilityDeploymentId, chatDeploymentId, clientName, connectionName);

        return deployment ?? throw new InvalidOperationException($"Unable to resolve a utility AI deployment using utilityDeploymentId '{utilityDeploymentId ?? "(null)"}', chatDeploymentId '{chatDeploymentId ?? "(null)"}', clientName '{clientName ?? "(null)"}', and connectionName '{connectionName ?? "(null)"}'.");
    }
}
