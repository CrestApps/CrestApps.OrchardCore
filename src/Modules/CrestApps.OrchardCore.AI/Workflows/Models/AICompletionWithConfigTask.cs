using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Fluid;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Liquid;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// A workflow task activity that performs AI completion using direct configuration parameters.
/// The full set of parameters is captured on an embedded <see cref="ChatInteraction"/> so that every
/// enabled feature can contribute its own configuration through dedicated display drivers, mirroring
/// the Chat Interactions experience.
/// </summary>
public sealed class AICompletionWithConfigTask : TaskActivity<AICompletionWithConfigTask>
{
    private readonly IAICompletionContextBuilder _completionContextBuilder;
    private readonly IAICompletionService _completionService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigTask"/> class.
    /// </summary>
    /// <param name="completionContextBuilder">The completion context builder that populates the AI completion context from the configured interaction.</param>
    /// <param name="completionService">The AI completion service used to invoke the model.</param>
    /// <param name="deploymentManager">The deployment manager for resolving deployments.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager for rendering prompt templates.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public AICompletionWithConfigTask(
        IAICompletionContextBuilder completionContextBuilder,
        IAICompletionService completionService,
        IAIDeploymentManager deploymentManager,
        ILiquidTemplateManager liquidTemplateManager,
        ILogger<AICompletionWithConfigTask> logger,
        IStringLocalizer<AICompletionWithConfigTask> stringLocalizer)
    {
        _completionContextBuilder = completionContextBuilder;
        _completionService = completionService;
        _deploymentManager = deploymentManager;
        _liquidTemplateManager = liquidTemplateManager;
        _logger = logger;
        S = stringLocalizer;
    }

    public override LocalizedString DisplayText => S["AI Completion using Direct Config"];

    public override LocalizedString Category => S["Artificial Intelligence"];

    /// <summary>
    /// Gets or sets the interaction that carries the full set of AI parameters used to invoke the model.
    /// </summary>
    public ChatInteraction Interaction
    {
        get => GetProperty(() => new ChatInteraction());
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid prompt template used to generate the user prompt.
    /// </summary>
    public string PromptTemplate
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the property name used to store the AI response in the workflow output.
    /// </summary>
    public string ResultPropertyName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcomes(S["Done"], S["Drew Blank"], S["Failed"]);
    }

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var userPrompt = await _liquidTemplateManager.RenderStringAsync(PromptTemplate, NullEncoder.Default, null);

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("The generated prompt from the template is empty.");

            return Outcomes("Failed");
        }

        try
        {
            var interaction = Interaction;

            var context = await _completionContextBuilder.BuildAsync(interaction);

            var deployment = await _deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentPurpose.Chat,
                deploymentName: context.ChatDeploymentName);

            if (deployment == null || string.IsNullOrEmpty(deployment.ConnectionName))
            {
                _logger.LogWarning("Unable to resolve the selected chat deployment with a valid connection. Deployment: '{DeploymentName}'.", context.ChatDeploymentName);

                return Outcomes("Failed");
            }

            var completion = await _completionService.CompleteAsync(deployment, [new ChatMessage(ChatRole.User, userPrompt.Trim())], context);

            var bestChoice = completion.Messages.FirstOrDefault();

            if (string.IsNullOrEmpty(bestChoice?.Text))
            {
                return Outcomes("Drew Blank");
            }

            var value = new AIResponseMessage
            {
                Content = bestChoice.Text,
            };

            workflowContext.Output[ResultPropertyName ?? "ChatResponse"] = value;

            return Outcomes("Done");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while completing the AI task.");

            return Outcomes("Failed");
        }
    }
}
