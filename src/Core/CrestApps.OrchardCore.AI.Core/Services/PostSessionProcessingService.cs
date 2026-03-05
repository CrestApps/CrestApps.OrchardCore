using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Processes configured post-session tasks after a chat session is closed.
/// Analyzes the full conversation transcript using AI to produce structured results
/// such as disposition, summary, or sentiment.
/// </summary>
public sealed class PostSessionProcessingService
{
    private readonly IAIClientFactory _clientFactory;
    private readonly IAIToolsService _toolsService;
    private readonly IAITemplateService _aiTemplateService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly AIProviderOptions _providerOptions;
    private readonly DefaultAIOptions _defaultOptions;
    private readonly ILoggerFactory _loggerFactory;

    public PostSessionProcessingService(
        IAIClientFactory clientFactory,
        IAIToolsService toolsService,
        IAITemplateService aiTemplateService,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<DefaultAIOptions> defaultOptions,
        IServiceProvider serviceProvider,
        IClock clock,
        ILoggerFactory loggerFactory)
    {
        _clientFactory = clientFactory;
        _toolsService = toolsService;
        _aiTemplateService = aiTemplateService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _providerOptions = providerOptions.Value;
        _defaultOptions = defaultOptions.Value;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Uses AI to determine whether the conversation was semantically resolved,
    /// regardless of how the session was closed (natural farewell, inactivity, etc.).
    /// Returns <see langword="true"/> if the AI determines the user's query was addressed.
    /// </summary>
    public async Task<bool> EvaluateResolutionAsync(
        AIProfile profile,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        CancellationToken cancellationToken = default)
    {
        if (!prompts.Any(p => p.Role == ChatRole.User))
        {
            return false;
        }

        var chatClient = await GetChatClientAsync(profile);

        if (chatClient == null)
        {
            throw new InvalidOperationException(
                $"Unable to create a chat client for resolution analysis on profile '{profile.ItemId}'.");
        }

        var transcript = await RenderTranscriptAsync(AITemplateIds.ResolutionAnalysisPrompt, prompts);

        if (string.IsNullOrEmpty(transcript))
        {
            return false;
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, await _aiTemplateService.RenderAsync(AITemplateIds.ResolutionAnalysis)),
            new(ChatRole.User, transcript),
        };

        var response = await chatClient.GetResponseAsync<ResolutionAnalysisResponse>(messages, new ChatOptions
        {
            Temperature = 0f,
        }, null, cancellationToken);

        return response.Result?.Resolved ?? false;
    }

    /// <summary>
    /// Evaluates the conversation against configured conversion goals using AI.
    /// Returns a list of goal results with scores, or null if evaluation fails.
    /// </summary>
    public async Task<List<ConversionGoalResult>> EvaluateConversionGoalsAsync(
        AIProfile profile,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        List<ConversionGoal> goals,
        CancellationToken cancellationToken = default)
    {
        if (goals is null || goals.Count == 0 || !prompts.Any(p => p.Role == ChatRole.User))
        {
            return null;
        }

        var chatClient = await GetChatClientAsync(profile);

        if (chatClient == null)
        {
            throw new InvalidOperationException(
                $"Unable to create a chat client for conversion goal evaluation on profile '{profile.ItemId}'.");
        }

        var arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["goals"] = goals.Select(g => new
            {
                g.Name,
                g.Description,
                g.MinScore,
                g.MaxScore,
            }).ToList(),
            ["prompts"] = ProjectPrompts(prompts),
        };

        var userPrompt = await _aiTemplateService.RenderAsync(AITemplateIds.ConversionGoalEvaluationPrompt, arguments);

        if (string.IsNullOrEmpty(userPrompt))
        {
            return null;
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, await _aiTemplateService.RenderAsync(AITemplateIds.ConversionGoalEvaluation)),
            new(ChatRole.User, userPrompt),
        };

        var response = await chatClient.GetResponseAsync<ConversionGoalEvaluationResponse>(messages, new ChatOptions
        {
            Temperature = 0f,
        }, null, cancellationToken);

        if (response.Result?.Goals is null || response.Result.Goals.Count == 0)
        {
            return null;
        }

        var results = new List<ConversionGoalResult>();

        foreach (var result in response.Result.Goals)
        {
            var goal = goals.FirstOrDefault(g =>
                string.Equals(g.Name, result.Name, StringComparison.OrdinalIgnoreCase));

            if (goal == null)
            {
                continue;
            }

            // Clamp score to valid range.
            var score = Math.Clamp(result.Score, goal.MinScore, goal.MaxScore);

            results.Add(new ConversionGoalResult
            {
                Name = goal.Name,
                Score = score,
                MaxScore = goal.MaxScore,
                Reasoning = result.Reasoning,
            });
        }

        return results;
    }

    /// <summary>
    /// Runs all configured post-session tasks against the closed session.
    /// Returns the results keyed by task name, or null if processing is not enabled.
    /// </summary>
    public async Task<Dictionary<string, PostSessionResult>> ProcessAsync(
        AIProfile profile,
        AIChatSession session,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        CancellationToken cancellationToken = default)
    {
        var settings = profile.GetSettings<AIProfilePostSessionSettings>();

        if (!settings.EnablePostSessionProcessing || settings.PostSessionTasks.Count == 0)
        {
            return null;
        }

        var chatClient = await GetChatClientAsync(profile);

        if (chatClient == null)
        {
            throw new InvalidOperationException(
                $"Unable to create a chat client for post-session processing on profile '{profile.ItemId}'.");
        }

        var arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tasks"] = settings.PostSessionTasks.Select(t => new
            {
                t.Name,
                Type = t.Type.ToString(),
                t.Instructions,
                t.AllowMultipleValues,
                Options = t.Options?.Select(o => new { o.Value, o.Description }).ToList(),
            }).ToList(),
            ["prompts"] = ProjectPrompts(prompts),
        };

        var prompt = await _aiTemplateService.RenderAsync(AITemplateIds.PostSessionAnalysisPrompt, arguments);

        if (string.IsNullOrEmpty(prompt))
        {
            return null;
        }

        var systemPrompt = await _aiTemplateService.RenderAsync(AITemplateIds.PostSessionAnalysis);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, prompt),
        };

        var tools = await ResolveToolsAsync(settings.ToolNames);

        // When tools are configured (e.g., sendEmail), use non-generic GetResponseAsync
        // to allow tool execution. The generic version uses structured output which
        // conflicts with tool calls — the model may fail to call tools when forced
        // to produce structured JSON output.
        if (tools is not null && tools.Count > 0)
        {
            return await ProcessWithToolsAsync(chatClient, messages, tools, settings.PostSessionTasks, cancellationToken);
        }

        var response = await chatClient.GetResponseAsync<PostSessionProcessingResponse>(messages, new ChatOptions
        {
            Temperature = 0f,
        }, null, cancellationToken);

        if (response.Result?.Tasks == null || response.Result.Tasks.Count == 0)
        {
            return null;
        }

        return ApplyResults(settings.PostSessionTasks, response.Result.Tasks);
    }

    private async Task<Dictionary<string, PostSessionResult>> ProcessWithToolsAsync(
        IChatClient chatClient,
        List<ChatMessage> messages,
        IList<AITool> tools,
        List<PostSessionTask> tasks,
        CancellationToken cancellationToken)
    {
        // Wrap the raw client with FunctionInvokingChatClient so that tool_call
        // messages returned by the model are actually executed (e.g., sendEmail).
        var client = chatClient
            .AsBuilder()
            .UseFunctionInvocation(_loggerFactory, c =>
            {
                c.MaximumIterationsPerRequest = _defaultOptions.MaximumIterationsPerRequest;
            })
            .Build(_serviceProvider);

        var response = await client.GetResponseAsync(messages, new ChatOptions
        {
            Temperature = 0f,
            Tools = tools,
        }, cancellationToken);

        var responseText = response.Messages?.LastOrDefault()?.Text?.Trim();

        if (string.IsNullOrEmpty(responseText))
        {
            return null;
        }

        // Try to parse the response as structured JSON.
        // When tools are involved, the model may return plain text after tool execution
        // instead of structured JSON — this is expected behavior.
        PostSessionProcessingResponse result = null;

        try
        {
            result = System.Text.Json.JsonSerializer.Deserialize<PostSessionProcessingResponse>(
                responseText, JSOptions.CaseInsensitive);
        }
        catch (System.Text.Json.JsonException)
        {
            // Model returned text after executing tools. Tools have already run.
        }

        if (result?.Tasks == null || result.Tasks.Count == 0)
        {
            return null;
        }

        return ApplyResults(tasks, result.Tasks);
    }

    private Dictionary<string, PostSessionResult> ApplyResults(
        List<PostSessionTask> tasks,
        List<PostSessionTaskResult> results)
    {
        var now = _clock.UtcNow;
        var applied = new Dictionary<string, PostSessionResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
        {
            if (string.IsNullOrWhiteSpace(result.Name) || string.IsNullOrWhiteSpace(result.Value))
            {
                continue;
            }

            var task = tasks.FirstOrDefault(t =>
                string.Equals(t.Name, result.Name, StringComparison.OrdinalIgnoreCase));

            if (task == null)
            {
                continue;
            }

            // For PredefinedOptions type, validate the value(s) against the configured options.
            if (task.Type == PostSessionTaskType.PredefinedOptions && task.Options.Count > 0)
            {
                var optionValues = task.Options.Select(o => o.Value).ToList();

                if (task.AllowMultipleValues)
                {
                    // Validate each comma-separated value.
                    var selectedValues = result.Value
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    var validValues = selectedValues
                        .Where(v => optionValues.Any(o => string.Equals(o, v, StringComparison.OrdinalIgnoreCase)))
                        .Select(v => optionValues.First(o => string.Equals(o, v, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (validValues.Count == 0)
                    {
                        continue;
                    }

                    result.Value = string.Join(", ", validValues);
                }
                else
                {
                    var matchedOption = optionValues.FirstOrDefault(o =>
                        string.Equals(o, result.Value, StringComparison.OrdinalIgnoreCase));

                    if (matchedOption == null)
                    {
                        continue;
                    }

                    result.Value = matchedOption;
                }
            }

            applied[task.Name] = new PostSessionResult
            {
                Name = task.Name,
                Value = result.Value,
                ProcessedAtUtc = now,
            };
        }

        return applied;
    }

    private async Task<IChatClient> GetChatClientAsync(AIProfile profile)
    {
        if (!_providerOptions.Providers.TryGetValue(profile.Source, out var provider))
        {
            return null;
        }

        var connectionName = !string.IsNullOrEmpty(profile.ConnectionName)
            ? profile.ConnectionName
            : provider.DefaultConnectionName;

        if (string.IsNullOrEmpty(connectionName) || !provider.Connections.TryGetValue(connectionName, out var connection))
        {
            return null;
        }

        var deploymentName = connection.GetUtilityDeploymentName(throwException: false);

        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = connection.GetChatDeploymentName(throwException: false);
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            return null;
        }

        return await _clientFactory.CreateChatClientAsync(profile.Source, connectionName, deploymentName);
    }

    private async Task<IList<AITool>> ResolveToolsAsync(string[] toolNames)
    {
        if (toolNames is null || toolNames.Length == 0)
        {
            return null;
        }

        var tools = new List<AITool>();

        foreach (var name in toolNames)
        {
            var tool = await _toolsService.GetByNameAsync(name);

            if (tool is not null)
            {
                tools.Add(tool);
            }
        }

        return tools.Count > 0 ? tools : null;
    }

    private async Task<string> RenderTranscriptAsync(
        string templateId,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        Dictionary<string, object> extraArguments = null)
    {
        var arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["prompts"] = ProjectPrompts(prompts),
        };

        if (extraArguments is not null)
        {
            foreach (var kvp in extraArguments)
            {
                arguments[kvp.Key] = kvp.Value;
            }
        }

        return await _aiTemplateService.RenderAsync(templateId, arguments);
    }

    private static List<object> ProjectPrompts(IReadOnlyList<AIChatSessionPrompt> prompts)
    {
        return prompts
            .Where(p => !p.IsGeneratedPrompt)
            .Select(p => new
            {
                Role = p.Role == ChatRole.User ? "User" : "Assistant",
                Content = p.Content?.Trim(),
            })
            .Cast<object>()
            .ToList();
    }
}

public sealed class PostSessionProcessingResponse
{
    public List<PostSessionTaskResult> Tasks { get; set; } = [];
}

public sealed class PostSessionTaskResult
{
    public string Name { get; set; }

    public string Value { get; set; }
}

public sealed class ResolutionAnalysisResponse
{
    public bool Resolved { get; set; }
}

public sealed class ConversionGoalEvaluationResponse
{
    public List<ConversionGoalEvaluationResult> Goals { get; set; } = [];
}

public sealed class ConversionGoalEvaluationResult
{
    public string Name { get; set; }

    public int Score { get; set; }

    public string Reasoning { get; set; }
}
