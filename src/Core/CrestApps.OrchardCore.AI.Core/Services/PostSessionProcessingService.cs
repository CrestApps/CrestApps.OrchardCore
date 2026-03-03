using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Models;
using J2N.Text;
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
    private readonly IClock _clock;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public PostSessionProcessingService(
        IAIClientFactory clientFactory,
        IAIToolsService toolsService,
        IAITemplateService aiTemplateService,
        IOptions<AIProviderOptions> providerOptions,
        IClock clock,
        ILogger<PostSessionProcessingService> logger)
    {
        _clientFactory = clientFactory;
        _toolsService = toolsService;
        _aiTemplateService = aiTemplateService;
        _clock = clock;
        _providerOptions = providerOptions.Value;
        _logger = logger;
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

        try
        {
            var chatClient = await GetChatClientAsync(profile);

            if (chatClient == null)
            {
                return false;
            }

            var transcript = BuildConversationTranscript(prompts);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI resolution analysis failed for profile '{ProfileId}'.", profile.ItemId);
            return false;
        }
    }

    private static string BuildConversationTranscript(IReadOnlyList<AIChatSessionPrompt> prompts)
    {
        var builder = new StringBuffer("Conversation transcript:");

        foreach (var prompt in prompts)
        {
            if (prompt.IsGeneratedPrompt)
            {
                continue;
            }

            builder.AppendLine();
            builder.Append(prompt.Role == ChatRole.User ? "User: " : "Assistant: ");
            builder.Append(prompt.Content?.Trim());
        }

        return builder.ToString();
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

        try
        {
            var chatClient = await GetChatClientAsync(profile);

            if (chatClient == null)
            {
                return null;
            }

            var transcript = BuildConversationTranscript(prompts);

            if (string.IsNullOrEmpty(transcript))
            {
                return null;
            }

            var goalsPrompt = new StringBuffer("Evaluate the following conversation against each goal and assign a score.\n\nGoals:\n");

            foreach (var goal in goals)
            {
                goalsPrompt.Append("- ");
                goalsPrompt.Append(goal.Name);
                goalsPrompt.Append(": ");
                goalsPrompt.Append(goal.Description);
                goalsPrompt.Append(" (score range: ");
                goalsPrompt.Append(goal.MinScore.ToString());
                goalsPrompt.Append("-");
                goalsPrompt.Append(goal.MaxScore.ToString());
                goalsPrompt.Append(")");
                goalsPrompt.AppendLine();
            }

            goalsPrompt.AppendLine();
            goalsPrompt.Append(transcript);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, await _aiTemplateService.RenderAsync(AITemplateIds.ConversionGoalEvaluation)),
                new(ChatRole.User, goalsPrompt.ToString()),
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversion goal evaluation failed for profile '{ProfileId}'.", profile.ItemId);
            return null;
        }
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

        try
        {
            var chatClient = await GetChatClientAsync(profile);

            if (chatClient == null)
            {
                _logger.LogWarning("Unable to create a chat client for post-session processing on profile '{ProfileId}'.", profile.ItemId);
                return null;
            }

            var prompt = BuildProcessingPrompt(settings.PostSessionTasks, prompts);

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

            var response = await chatClient.GetResponseAsync<PostSessionProcessingResponse>(messages, new ChatOptions
            {
                Temperature = 0f,
                Tools = await ResolveToolsAsync(settings.ToolNames),
            }, null, cancellationToken);

            if (response.Result?.Tasks == null || response.Result.Tasks.Count == 0)
            {
                return null;
            }

            return ApplyResults(settings.PostSessionTasks, response.Result.Tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-session processing failed for session '{SessionId}'.", session.SessionId);
            return null;
        }
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

    private static string BuildProcessingPrompt(List<PostSessionTask> tasks, IReadOnlyList<AIChatSessionPrompt> prompts)
    {
        var builder = new StringBuffer("Analyze the following completed chat conversation and produce results for the requested tasks.");

        builder.AppendLine();
        builder.Append("Tasks to process:");

        foreach (var task in tasks)
        {
            builder.AppendLine();
            builder.Append("- ");
            builder.Append(task.Name);
            builder.Append(" (type: ");
            builder.Append(task.Type.ToString());
            builder.Append(")");

            if (!string.IsNullOrEmpty(task.Instructions))
            {
                builder.Append(": ");
                builder.Append(task.Instructions);
            }

            if (task.Type == PostSessionTaskType.PredefinedOptions && task.Options.Count > 0)
            {
                if (task.AllowMultipleValues)
                {
                    builder.Append(" [allowMultiple=true]");
                }

                builder.Append(" Options: [");

                for (var i = 0; i < task.Options.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(task.Options[i].Value);

                    if (!string.IsNullOrWhiteSpace(task.Options[i].Description))
                    {
                        builder.Append(" (");
                        builder.Append(task.Options[i].Description);
                        builder.Append(")");
                    }
                }

                builder.Append("]");
            }
        }

        // Build the conversation transcript.
        builder.AppendLine();
        builder.AppendLine();
        builder.Append("Conversation transcript:");

        foreach (var prompt in prompts)
        {
            if (prompt.IsGeneratedPrompt)
            {
                continue;
            }

            builder.AppendLine();
            builder.Append(prompt.Role == ChatRole.User ? "User: " : "Assistant: ");
            builder.Append(prompt.Content?.Trim());
        }

        return builder.ToString();
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
