using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Capability helpers shared by the editors that offer deployments to choose from.
/// </summary>
public static class AIDeploymentCapabilityServiceExtensions
{
    /// <summary>
    /// Keeps only the deployments that can hold a text conversation.
    /// </summary>
    /// <param name="capabilityService">The capability service.</param>
    /// <param name="deployments">The deployments to filter.</param>
    /// <remarks>
    /// A chat or utility deployment is asked for text completions, which a speech-to-speech model cannot
    /// serve — offering one in those pickers only lets an operator choose a deployment that fails at run
    /// time. The check is deliberately the lenient one: a deployment that has never declared capabilities
    /// is unconstrained, so it stays in the list rather than disappearing from every editor.
    /// </remarks>
    public static IEnumerable<AIDeployment> WhereCanHoldTextConversation(
        this IAIDeploymentCapabilityService capabilityService,
        IEnumerable<AIDeployment> deployments)
    {
        ArgumentNullException.ThrowIfNull(capabilityService);

        if (deployments is null)
        {
            return [];
        }

        return deployments.Where(deployment => capabilityService.SupportsFeatureOrUnconstrained(deployment, AIDeploymentFeatureNames.TextGeneration));
    }
}
