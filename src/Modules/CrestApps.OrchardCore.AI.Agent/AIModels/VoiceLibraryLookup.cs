using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Speech;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Finds the voice library of a text-to-speech deployment, the way the Voice library page does.
/// </summary>
internal static class VoiceLibraryLookup
{
    /// <summary>
    /// Finds a deployment's voice library.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="deploymentName">The technical name of the deployment, or <see langword="null"/> for the first text-to-speech deployment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deployment and its library, or the sentence that says why there is none.</returns>
    public static async Task<(AIDeployment Deployment, IVoiceLibrary Library, string Error)> FindAsync(IServiceProvider services, string deploymentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var capabilityService = services.GetService<IAIDeploymentCapabilityService>();
        IReadOnlyList<AIDeployment> speechDeployments = capabilityService is null
            ? []
            : await capabilityService.GetDeploymentsWithFeatureAsync(AIDeploymentFeatureNames.TextToSpeech, cancellationToken);

        AIDeployment deployment;

        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            deployment = speechDeployments.Count > 0 ? speechDeployments[0] : null;

            if (deployment is null)
            {
                return (null, null, "The site has no text-to-speech deployment, so there is no voice library. Add one with saveAIDeployment and the textToSpeech capability.");
            }
        }
        else
        {
            deployment = await services.GetRequiredService<IAIDeploymentManager>().FindByNameAsync(deploymentName.Trim(), cancellationToken);

            if (deployment is null)
            {
                return (null, null, $"No deployment is named '{deploymentName}'. {DescribeChoices(speechDeployments)}");
            }
        }

        var resolver = services.GetService<IVoiceLibraryResolver>();
        var library = resolver is null ? null : await resolver.GetLibraryAsync(deployment, cancellationToken);

        return library is null
            ? (deployment, null, $"The deployment '{deployment.Name}' has no voice library. {DescribeChoices(speechDeployments)}")
            : (deployment, library, null);
    }

    private static string DescribeChoices(IReadOnlyList<AIDeployment> speechDeployments)
        => speechDeployments.Count == 0
            ? "The site has no text-to-speech deployment."
            : $"The text-to-speech deployments are {string.Join(", ", speechDeployments.Select(deployment => deployment.Name))}.";
}
