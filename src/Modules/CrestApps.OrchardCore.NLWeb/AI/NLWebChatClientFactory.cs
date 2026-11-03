using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Resilience;
using CrestApps.OrchardCore.NLWeb.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// Creates the chat client the NLWeb AI steps use: the deployment the NLWeb settings select, or the default utility
/// deployment.
/// </summary>
internal sealed class NLWebChatClientFactory
{
    /// <summary>
    /// The usage purpose recorded for the AI calls of NLWeb.
    /// </summary>
    internal const string UsagePurpose = "NLWeb";

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebChatClientFactory"/> class.
    /// </summary>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    public NLWebChatClientFactory(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory)
    {
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
    }

    /// <summary>
    /// Creates the chat client.
    /// </summary>
    /// <param name="settings">The NLWeb settings.</param>
    /// <returns>The chat client, or <see langword="null"/> when no deployment is available.</returns>
    public async Task<IChatClient> CreateAsync(NLWebSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var deployment = await _deploymentManager.ResolveUtilityOrDefaultAsync(utilityDeploymentName: settings.DeploymentName);

        if (deployment is null)
        {
            return null;
        }

        return await _clientFactory.CreateChatClientAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: UsagePurpose));
    }
}
