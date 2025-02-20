using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
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

public sealed class AICompletionTask : TaskActivity<AICompletionTask>
{
    private readonly IAIProfileManager _profileManager;
    private readonly IAICompletionService _completionService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public AICompletionTask(
        IAIProfileManager profileManager,
        IAICompletionService completionService,
        ILiquidTemplateManager liquidTemplateManager,
        ILogger<AICompletionTask> logger,
        IStringLocalizer<AICompletionTask> stringLocalizer)
    {
        _profileManager = profileManager;
        _completionService = completionService;
        _liquidTemplateManager = liquidTemplateManager;
        _logger = logger;
        S = stringLocalizer;
    }

    public override LocalizedString DisplayText => S["AI Completion"];

    public override LocalizedString Category => S["Artificial Intelligence"];

    public string ProfileId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string PromptTemplate
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string ResultPropertyName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public bool IncludeHtmlResponse
    {
        get => GetProperty(() => false);
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
            var completion = await _completionService.CompleteAsync(profile.Source, [new ChatMessage(ChatRole.User, userPrompt.Trim())], new AICompletionContext()
            {
                Profile = profile,
                UserMarkdownInResponse = IncludeHtmlResponse,
            });

            var bestChoice = completion.Choices.FirstOrDefault();

            if (string.IsNullOrEmpty(bestChoice?.Text))
            {
                return Outcomes("Drew Blank");
            }

            var value = new AIResponseMessage
            {
                Content = bestChoice.Text,
            };

            workflowContext.Output[ResultPropertyName] = value;

            return Outcomes("Done");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while completing the AI task.");

            return Outcomes("Failed");
        }
    }
}
