using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// Runs one prompt template against the site's utility (or default) AI deployment for the authoring assistant and the
/// scoring help. Every call is metered under <see cref="LearningAIConstants.UsagePurpose"/>.
/// </summary>
public interface ILearningAICompleter
{
    /// <summary>
    /// Gets whether an AI deployment is set up, so the assistants can say so instead of failing.
    /// </summary>
    /// <returns><see langword="true"/> when a model can be called.</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Renders a prompt template as the system message, sends the input and returns the model's answer.
    /// </summary>
    /// <param name="templateId">The prompt template.</param>
    /// <param name="arguments">The template's arguments.</param>
    /// <param name="input">The input, sent as the user message.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The answer, or <see langword="null"/> when no deployment is set up.</returns>
    Task<string> CompleteAsync(string templateId, IDictionary<string, object> arguments, string input, CancellationToken cancellationToken);
}

/// <summary>
/// The default <see cref="ILearningAICompleter"/>.
/// </summary>
internal sealed class LearningAICompleter : ILearningAICompleter
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly ITemplateService _templateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningAICompleter"/> class.
    /// </summary>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    /// <param name="templateService">The template service.</param>
    public LearningAICompleter(
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
            .UseUsageLabels(purpose: LearningAIConstants.UsagePurpose));

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
