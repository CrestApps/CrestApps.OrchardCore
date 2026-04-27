using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
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

/// <summary>
/// A workflow task activity that performs AI completion using direct configuration parameters.
/// </summary>
public sealed class AICompletionWithConfigTask : TaskActivity<AICompletionWithConfigTask>
{
    private readonly IAIClientFactory _aIClientFactory;
    private readonly IAIToolsService _aIToolsService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly DefaultAIOptions _defaultOptions;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigTask"/> class.
    /// </summary>
    /// <param name="aIClientFactory">The AI client factory for creating chat clients.</param>
    /// <param name="aIToolsService">The AI tools service for resolving tool definitions.</param>
    /// <param name="deploymentManager">The deployment manager for resolving deployments.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager for rendering prompt templates.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="defaultOptions">The default AI options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
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

    /// <summary>
    /// Gets or sets the deployment name used for the AI completion.
    /// </summary>
    public string DeploymentName
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
    /// Gets or sets the system message sent to the AI model.
    /// </summary>
    public string SystemMessage
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the frequency penalty for the AI completion.
    /// </summary>
    public float? FrequencyPenalty
    {
        get => GetProperty<float?>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the presence penalty for the AI completion.
    /// </summary>
    public float? PresencePenalty
    {
        get => GetProperty<float?>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the temperature for the AI completion.
    /// </summary>
    public float? Temperature
    {
        get => GetProperty<float?>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the top-P (nucleus sampling) value for the AI completion.
    /// </summary>
    public float? TopP
    {
        get => GetProperty<float?>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the maximum number of tokens for the AI completion output.
    /// </summary>
    public int? MaxTokens
    {
        get => GetProperty<int?>();
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

    /// <summary>
    /// Gets or sets the names of the AI tools to enable for this task.
    /// </summary>
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

            var client = await _aIClientFactory.CreateChatClientAsync(deployment);

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
