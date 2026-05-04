using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Liquid;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;

using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// A workflow task activity that performs AI completion using an AI profile.
/// </summary>
public sealed class AICompletionFromProfileTask : TaskActivity<AICompletionFromProfileTask>
{
    private readonly INamedCatalogManager<AIProfile> _profileManager;
    private readonly IAICompletionService _completionService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IAICompletionContextBuilder _completionContextBuilder;

    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionFromProfileTask"/> class.
    /// </summary>
    /// <param name="profileManager">The profile manager for resolving AI profiles.</param>
    /// <param name="completionService">The AI completion service.</param>
    /// <param name="deploymentManager">The deployment manager for resolving deployments.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager for rendering prompt templates.</param>
    /// <param name="completionContextBuilder">The completion context builder.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public AICompletionFromProfileTask(
        INamedCatalogManager<AIProfile> profileManager,
        IAICompletionService completionService,
        IAIDeploymentManager deploymentManager,
        ILiquidTemplateManager liquidTemplateManager,
        IAICompletionContextBuilder completionContextBuilder,
        ILogger<AICompletionFromProfileTask> logger,
        IStringLocalizer<AICompletionFromProfileTask> stringLocalizer)
    {
        _profileManager = profileManager;
        _completionService = completionService;
        _deploymentManager = deploymentManager;
        _liquidTemplateManager = liquidTemplateManager;
        _completionContextBuilder = completionContextBuilder;
        _logger = logger;
        S = stringLocalizer;
    }

    public override LocalizedString DisplayText => S["AI Completion using Profile"];

    public override LocalizedString Category => S["Artificial Intelligence"];

    /// <summary>
    /// Gets or sets the AI profile identifier to use for the completion.
    /// </summary>
    public string ProfileId
    {
        get => GetProperty<string>();
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
        var profile = await _profileManager.FindByIdAsync(ProfileId);

        if (profile is null)
        {
            return Outcomes("Failed");
        }

        var userPrompt = await _liquidTemplateManager.RenderStringAsync(PromptTemplate, NullEncoder.Default,
        new Dictionary<string, FluidValue>()
        {
            ["Profile"] = new ObjectValue(profile),
        });

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("The generated prompt from the template is empty.");

            return Outcomes("Failed");
        }

        try
        {
            var context = await _completionContextBuilder.BuildAsync(profile);
            var deployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentName: context.ChatDeploymentName)

            ?? throw new InvalidOperationException("Unable to resolve a chat deployment for the profile.");

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
