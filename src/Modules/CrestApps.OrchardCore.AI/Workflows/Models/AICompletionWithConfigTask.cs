using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Fluid;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Liquid;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

public sealed class AICompletionWithConfigTask : TaskActivity<AICompletionWithConfigTask>
{
    private readonly IAIClientFactory _aIClientFactory;
    private readonly IAIToolsService _aIToolsService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly DefaultAIOptions _defaultOptions;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public AICompletionWithConfigTask(
        IAIClientFactory aIClientFactory,
        IAIToolsService aIToolsService,
        IAIDeploymentManager deploymentManager,
        ILiquidTemplateManager liquidTemplateManager,
        IServiceProvider serviceProvider,
        DefaultAIOptions defaultOptions,
        ILogger<AICompletionWithConfigTask> logger,
        IStringLocalizer<AICompletionWithConfigTask> stringLocalizer)
    {
        _aIClientFactory = aIClientFactory;
        _aIToolsService = aIToolsService;
        _deploymentManager = deploymentManager;
        _liquidTemplateManager = liquidTemplateManager;
        _defaultOptions = defaultOptions;
        ServiceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    internal IServiceProvider ServiceProvider { get; }

    public override LocalizedString DisplayText => S["AI Completion using Direct Config"];

    public override LocalizedString Category => S["Artificial Intelligence"];

    public string DeploymentName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string PromptTemplate
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string SystemMessage
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public float? FrequencyPenalty
    {
        get => GetProperty<float?>();
        set => SetProperty(value);
    }

    public float? PresencePenalty
    {
        get => GetProperty<float?>();
        set => SetProperty(value);
    }

    public float? Temperature
    {
        get => GetProperty<float?>();
        set => SetProperty(value);
    }

    public float? TopP
    {
        get => GetProperty<float?>();
        set => SetProperty(value);
    }

    public int? MaxTokens
    {
        get => GetProperty<int?>();
        set => SetProperty(value);
    }

    public string ResultPropertyName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string[] ToolNames
    {
        get => GetProperty<string[]>();
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
            var deployment = await _deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentType.Chat,
                deploymentName: DeploymentName);

            if (deployment == null || string.IsNullOrEmpty(deployment.ConnectionName))
            {
                _logger.LogWarning("Unable to resolve the selected chat deployment with a valid connection. Deployment: '{DeploymentName}'.", DeploymentName);
                return Outcomes("Failed");
            }

            var client = await _aIClientFactory.CreateChatClientAsync(deployment.ClientName, deployment.ConnectionName, deployment.ModelName);

            var chatOptions = new ChatOptions
            {
                FrequencyPenalty = FrequencyPenalty,
                PresencePenalty = PresencePenalty,
                Temperature = Temperature,
                TopP = TopP,
                MaxOutputTokens = MaxTokens,
            };

            if (ToolNames is not null && ToolNames.Length > 0)
            {
                chatOptions.Tools = [];
                chatOptions.ToolMode = ChatToolMode.Auto;

                client = client
                    .AsBuilder()
                    .UseFunctionInvocation(ServiceProvider.GetRequiredService<ILoggerFactory>(), c =>
                    {
                        c.MaximumIterationsPerRequest = _defaultOptions.MaximumIterationsPerRequest;
                    }).Build(ServiceProvider);

                foreach (var toolName in ToolNames)
                {
                    var tool = await _aIToolsService.GetByNameAsync(toolName);

                    if (tool is null)
                    {
                        continue;
                    }

                    chatOptions.Tools.Add(tool);
                }
            }

            var messages = new List<ChatMessage>();

            if (!string.IsNullOrWhiteSpace(SystemMessage))
            {
                messages.Add(new ChatMessage(ChatRole.System, SystemMessage.Trim()));
            }

            messages.Add(new ChatMessage(ChatRole.User, userPrompt.Trim()));

            var completion = await client.GetResponseAsync(messages, chatOptions);

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
