using CrestApps.OrchardCore.OpenAI.Models;
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

namespace CrestApps.OrchardCore.OpenAI.Workflows.Models;

public sealed class ChatUtilityCompletionTask : TaskActivity<ChatUtilityCompletionTask>
{
    private readonly IOpenAIChatProfileManager _chatProfileManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IOpenAIMarkdownService _markdownService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public ChatUtilityCompletionTask(
        IOpenAIChatProfileManager chatProfileManager,
        IServiceProvider serviceProvider,
        ILiquidTemplateManager liquidTemplateManager,
        IOpenAIMarkdownService markdownService,
        ILogger<ChatUtilityCompletionTask> logger,
        IStringLocalizer<ChatUtilityCompletionTask> stringLocalizer)
    {
        _chatProfileManager = chatProfileManager;
        _serviceProvider = serviceProvider;
        _liquidTemplateManager = liquidTemplateManager;
        _markdownService = markdownService;
        _logger = logger;
        S = stringLocalizer;
    }

    public override LocalizedString DisplayText => S["Chat Utility Completion"];

    public override LocalizedString Category => S["OpenAI"];

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
        var profile = await _chatProfileManager.FindByIdAsync(ProfileId);

        if (profile is null)
        {
            return Outcomes("Failed");
        }

        if (profile.Type != OpenAIChatProfileType.Utility)
        {
            _logger.LogWarning("The requested profile '{ProfileId}' has a type of '{ProfileType}', but it must be of type 'Utility' to use the Chat Utility Completion Task.", profile.Id, profile.Type.ToString());

            return Outcomes("Failed");
        }

        var completionService = _serviceProvider.GetKeyedService<IOpenAIChatCompletionService>(profile.Source);

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

        var completion = await completionService.ChatAsync([new ChatMessage(ChatRole.User, userPrompt.Trim())], new OpenAIChatCompletionContext(profile)
        {
            SystemMessage = profile.SystemMessage,
            UserMarkdownInResponse = IncludeHtmlResponse,
        });

        var bestChoice = completion.Choices.FirstOrDefault();

        if (string.IsNullOrEmpty(bestChoice?.Content))
        {
            return Outcomes("Drew Blank");
        }

        var value = new OpenAIChatResponseMessage
        {
            Content = bestChoice.Content,
        };

        if (IncludeHtmlResponse)
        {
            value.HtmlContent = _markdownService.ToHtml(bestChoice.Content);
        }

        workflowContext.Output[ResultPropertyName] = value;

        return Outcomes("Done");
    }
}
