using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Sends prompts to the site's utility AI deployment, or its default one.
/// </summary>
internal sealed class SurveysAICompleter : ISurveysAICompleter
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly ITemplateService _templateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysAICompleter"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    /// <param name="templateService">The prompt template service.</param>
    public SurveysAICompleter(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        ITemplateService templateService)
    {
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
        _templateService = templateService;
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync()
        => await _deploymentManager.ResolveUtilityOrDefaultAsync() is not null;

    /// <inheritdoc />
    public async Task<string> CompleteAsync(string templateId, IDictionary<string, object> arguments, string input, CancellationToken cancellationToken)
    {
        var deployment = await _deploymentManager.ResolveUtilityOrDefaultAsync();

        if (deployment is null)
        {
            return null;
        }

        var client = await _clientFactory.CreateChatClientAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: SurveysAIConstants.UsagePurpose));

        var systemPrompt = await _templateService.RenderAsync(templateId, arguments ?? new Dictionary<string, object>(), cancellationToken);
        var response = await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, input ?? string.Empty),
            ],
            cancellationToken: cancellationToken);

        return response.Text?.Trim();
    }
}
