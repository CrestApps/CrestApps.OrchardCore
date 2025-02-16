using CrestApps.OrchardCore.AI.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IAIMarkdownService _markdownService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public AICompletionTask(
        IAIProfileManager profileManager,
        IServiceProvider serviceProvider,
        ILiquidTemplateManager liquidTemplateManager,
        IAIMarkdownService markdownService,
        ILogger<AICompletionTask> logger,
        IStringLocalizer<AICompletionTask> stringLocalizer)
    {
        _profileManager = profileManager;
        _serviceProvider = serviceProvider;
        _liquidTemplateManager = liquidTemplateManager;
        _markdownService = markdownService;
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

        var completionService = _serviceProvider.GetKeyedService<IAICompletionService>(profile.Source);

        if (completionService is null)
        {
            _logger.LogError("Unable to find a chat completion service for the source: '{Source}'.", profile.Source);

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

        var completion = await completionService.CompleteAsync([new ChatMessage(ChatRole.User, userPrompt.Trim())], new AICompletionContext()
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

        if (IncludeHtmlResponse)
        {
            value.HtmlContent = _markdownService.ToHtml(bestChoice.Text);
        }

        workflowContext.Output[ResultPropertyName] = value;

        return Outcomes("Done");
    }
}
