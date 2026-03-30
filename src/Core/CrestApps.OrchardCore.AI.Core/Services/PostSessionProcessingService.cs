using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIToolsService _toolsService;
    private readonly IAITemplateService _aiTemplateService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly AIProviderOptions _providerOptions;
    private readonly DefaultAIOptions _defaultOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public PostSessionProcessingService(
        IAIClientFactory clientFactory,
        IAIToolsService toolsService,
        IAITemplateService aiTemplateService,
        IOptions<AIProviderOptions> providerOptions,
        DefaultAIOptions defaultOptions,
        IServiceProvider serviceProvider,
        IClock clock,
        ILoggerFactory loggerFactory,
        IAIDeploymentManager deploymentManager = null)
    {
        _clientFactory = clientFactory;
        _deploymentManager = deploymentManager;
        _toolsService = toolsService;
        _aiTemplateService = aiTemplateService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _providerOptions = providerOptions.Value;
        _defaultOptions = defaultOptions;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PostSessionProcessingService>();
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
    /// Tasks that have already succeeded (tracked in <see cref="AIChatSession.PostSessionResults"/>)
    /// are excluded from processing. Returns the results keyed by task name, or null if processing
    /// is not enabled or all tasks have already succeeded.
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
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Post-session processing skipped for session '{SessionId}': Enabled={Enabled}, TaskCount={TaskCount}.",
                    session.SessionId,
                    settings.EnablePostSessionProcessing,
                    settings.PostSessionTasks.Count);
            }

            return null;
        }

        // Filter out tasks that have already succeeded from a previous attempt.
        var tasksToProcess = settings.PostSessionTasks
            .Where(t => !session.PostSessionResults.TryGetValue(t.Name, out var existing)
                || existing.Status != PostSessionTaskResultStatus.Succeeded)
            .ToList();

        if (tasksToProcess.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Post-session processing skipped for session '{SessionId}': all {TaskCount} task(s) have already succeeded.",
                    session.SessionId,
                    settings.PostSessionTasks.Count);
            }

            return null;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Post-session processing for session '{SessionId}': {PendingCount}/{TotalCount} task(s) to process: [{TaskNames}].",
                session.SessionId,
                tasksToProcess.Count,
                settings.PostSessionTasks.Count,
                string.Join(", ", tasksToProcess.Select(t => t.Name)));
        }

        var chatClient = await GetChatClientAsync(profile);

        if (chatClient == null)
        {
            throw new InvalidOperationException(
                $"Unable to create a chat client for post-session processing on profile '{profile.ItemId}'.");
        }

        var arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tasks"] = tasksToProcess.Select(t => new
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
            _logger.LogWarning(
                "Post-session processing aborted for session '{SessionId}': rendered user prompt is empty. Template='{TemplateId}'.",
                session.SessionId,
                AITemplateIds.PostSessionAnalysisPrompt);

            return null;
        }

        var systemPrompt = await _aiTemplateService.RenderAsync(AITemplateIds.PostSessionAnalysis);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, prompt),
        };

        var tools = await ResolveToolsAsync(session.SessionId, settings.ToolNames);

        // When tools are configured (e.g., sendEmail), use non-generic GetResponseAsync
        // to allow tool execution. The generic version uses structured output which
        // conflicts with tool calls — the model may fail to call tools when forced
        // to produce structured JSON output.
        if (tools is not null && tools.Count > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Post-session processing for session '{SessionId}' using tools path with {ToolCount} tool(s): [{ToolNames}].",
                    session.SessionId,
                    tools.Count,
                    string.Join(", ", tools.Select(t => t.Name)));
            }

            return await ProcessWithToolsAsync(session.SessionId, chatClient, messages, tools, tasksToProcess, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Post-session processing for session '{SessionId}' using structured output path (no tools configured or resolved).",
                session.SessionId);
        }

        var response = await chatClient.GetResponseAsync<PostSessionProcessingResponse>(messages, new ChatOptions
        {
            Temperature = 0f,
        }, null, cancellationToken);

        if (response.Result?.Tasks == null || response.Result.Tasks.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Post-session structured output for session '{SessionId}' returned no tasks.",
                    session.SessionId);
            }

            return null;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Post-session structured output for session '{SessionId}' returned {TaskCount} task result(s): [{TaskNames}].",
                session.SessionId,
                response.Result.Tasks.Count,
                string.Join(", ", response.Result.Tasks.Select(t => t.Name)));
        }

        return ApplyResults(tasksToProcess, response.Result.Tasks);
    }

    private async Task<Dictionary<string, PostSessionResult>> ProcessWithToolsAsync(
        string sessionId,
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

        // Log tool invocation details from the response messages.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var toolCallCount = response.Messages?
                .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? [])
                .Count() ?? 0;

            var toolResultCount = response.Messages?
                .SelectMany(m => m.Contents?.OfType<FunctionResultContent>() ?? [])
                .Count() ?? 0;

            _logger.LogDebug(
                "Post-session tools response for session '{SessionId}': MessageCount={MessageCount}, ToolCalls={ToolCallCount}, ToolResults={ToolResultCount}.",
                sessionId,
                response.Messages?.Count ?? 0,
                toolCallCount,
                toolResultCount);
        }

        // Extract the final assistant message text, ignoring intermediate tool
        // call and tool result messages. After FunctionInvokingChatClient resolves
        // all tool calls, the model produces a final assistant message with the JSON
        // task results — that is the only message we care about.
        var responseText = response.Messages?
            .LastOrDefault(m => m.Role == ChatRole.Assistant && !string.IsNullOrEmpty(m.Text))
            ?.Text?.Trim();

        // Always log the raw response text for troubleshooting.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Post-session tools raw response for session '{SessionId}': '{ResponseText}'.",
                sessionId,
                CreateResponseLogPreview(responseText));
        }

        if (!string.IsNullOrEmpty(responseText))
        {
            var result = TryParsePostSessionResponse(sessionId, responseText);

            if (result?.Tasks != null && result.Tasks.Count > 0)
            {
                return ApplyResults(tasks, result.Tasks);
            }
        }
        else if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Post-session tools response for session '{SessionId}' has no final text content. Attempting structured recovery from tool messages.",
                sessionId);
        }

        var recoveredResults = await TryRecoverStructuredToolsResponseAsync(
            sessionId,
            chatClient,
            messages,
            response.Messages,
            tasks,
            cancellationToken);

        if (recoveredResults is not null && recoveredResults.Count > 0)
        {
            return recoveredResults;
        }

        return CreateFailedResults(sessionId, tasks, responseText);
    }

    /// <summary>
    /// Attempts to parse the AI response text as a <see cref="PostSessionProcessingResponse"/>
    /// using progressively lenient strategies:
    /// 1. Direct JSON deserialization.
    /// 2. Extract JSON from markdown code fences (```json ... ```).
    /// 3. Extract the first JSON object from surrounding text.
    /// </summary>
    private PostSessionProcessingResponse TryParsePostSessionResponse(string sessionId, string responseText)
    {
        // Strategy 1: Direct JSON deserialization.
        try
        {
            var result = JsonSerializer.Deserialize<PostSessionProcessingResponse>(
                responseText, JSOptions.CaseInsensitive);

            if (result?.Tasks != null && result.Tasks.Count > 0)
            {
                return result;
            }
        }
        catch (JsonException)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Post-session response for session '{SessionId}' is not valid JSON. Trying fallback extraction.",
                    sessionId);
            }
        }

        // Strategy 2: Extract JSON from markdown code fences.
        var jsonBlock = ExtractJsonFromCodeFence(responseText);

        if (jsonBlock != null)
        {
            try
            {
                var result = JsonSerializer.Deserialize<PostSessionProcessingResponse>(
                    jsonBlock, JSOptions.CaseInsensitive);

                if (result?.Tasks != null && result.Tasks.Count > 0)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Post-session response for session '{SessionId}' parsed successfully from code fence.",
                            sessionId);
                    }

                    return result;
                }
            }
            catch (JsonException)
            {
                // Code fence content wasn't valid JSON either, continue to next strategy.
            }
        }

        // Strategy 3: Extract the first JSON object from surrounding text.
        var jsonObject = ExtractJsonObject(responseText);

        if (jsonObject != null && jsonObject != responseText)
        {
            try
            {
                var result = JsonSerializer.Deserialize<PostSessionProcessingResponse>(
                    jsonObject, JSOptions.CaseInsensitive);

                if (result?.Tasks != null && result.Tasks.Count > 0)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Post-session response for session '{SessionId}' parsed successfully from embedded JSON object.",
                            sessionId);
                    }

                    return result;
                }
            }
            catch (JsonException)
            {
                // Extracted text wasn't valid JSON either.
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Post-session response for session '{SessionId}' could not be parsed as structured JSON after all extraction attempts.",
                sessionId);
        }

        return null;
    }

    private async Task<Dictionary<string, PostSessionResult>> TryRecoverStructuredToolsResponseAsync(
        string sessionId,
        IChatClient chatClient,
        List<ChatMessage> requestMessages,
        IList<ChatMessage> responseMessages,
        List<PostSessionTask> tasks,
        CancellationToken cancellationToken)
    {
        var followUpMessages = new List<ChatMessage>(requestMessages);
        var trailingAssistantText = responseMessages?
            .LastOrDefault(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text));

        if (responseMessages is not null)
        {
            foreach (var responseMessage in responseMessages)
            {
                if (ReferenceEquals(responseMessage, trailingAssistantText))
                {
                    continue;
                }

                followUpMessages.Add(responseMessage);
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Attempting structured recovery for post-session tool response on session '{SessionId}' using the original post-session analysis context. TaskCount={TaskCount}.",
                sessionId,
                tasks.Count);
        }

        var response = await chatClient.GetResponseAsync<PostSessionProcessingResponse>(followUpMessages, new ChatOptions
        {
            Temperature = 0f,
        }, null, cancellationToken);

        var recoveryResponseText = response.Messages?
            .LastOrDefault(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
            ?.Text?.Trim();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Post-session structured recovery raw response for session '{SessionId}': '{ResponseText}'.",
                sessionId,
                CreateResponseLogPreview(recoveryResponseText));
        }

        PostSessionProcessingResponse result;

        try
        {
            result = response.Result;
        }
        catch (InvalidOperationException)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Structured recovery for post-session tool response on session '{SessionId}' did not return JSON content.",
                    sessionId);
            }

            return null;
        }
        catch (JsonException)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Structured recovery for post-session tool response on session '{SessionId}' returned invalid JSON content.",
                    sessionId);
            }

            return null;
        }

        if (result?.Tasks is null || result.Tasks.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Structured recovery for post-session tool response on session '{SessionId}' returned no task results.",
                    sessionId);
            }

            return null;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Structured recovery for post-session tool response on session '{SessionId}' succeeded with {TaskCount} task result(s).",
                sessionId,
                result.Tasks.Count);
        }

        return ApplyResults(tasks, result.Tasks);
    }

    private Dictionary<string, PostSessionResult> CreateFailedResults(
        string sessionId,
        List<PostSessionTask> tasks,
        string responseText)
    {
        var now = _clock.UtcNow;
        var errorMessage = string.IsNullOrWhiteSpace(responseText)
            ? "Tool execution completed, but the AI response did not contain the required structured JSON results."
            : "The AI response could not be parsed as structured JSON after tool execution.";

        _logger.LogWarning(
            "Post-session tool response for session '{SessionId}' failed structured parsing. Marking {TaskCount} task(s) as failed. ResponseLength={ResponseLength}.",
            sessionId,
            tasks.Count,
            responseText?.Length ?? 0);

        return tasks.ToDictionary(
            task => task.Name,
            task => new PostSessionResult
            {
                Name = task.Name,
                Status = PostSessionTaskResultStatus.Failed,
                ErrorMessage = errorMessage,
                ProcessedAtUtc = now,
            },
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts JSON content from markdown code fences (e.g., ```json ... ``` or ``` ... ```).
    /// </summary>
    private static string ExtractJsonFromCodeFence(string text)
    {
        var match = Regex.Match(text, @"```(?:json)?\s*\n?([\s\S]*?)\n?\s*```", RegexOptions.None, TimeSpan.FromSeconds(1));

        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Extracts the first balanced JSON object from text that may contain surrounding content.
    /// </summary>
    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');

        if (start < 0)
        {
            return null;
        }

        var end = text.LastIndexOf('}');

        if (end <= start)
        {
            return null;
        }

        return text[start..(end + 1)];
    }

    private static string CreateResponseLogPreview(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
        {
            return "(empty)";
        }

        var normalized = responseText
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

        return normalized.Length > 2000 ? normalized[..2000] + "..." : normalized;
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
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Post-session task result skipped: Name='{Name}', HasValue={HasValue}.",
                        result.Name ?? "(null)",
                        !string.IsNullOrWhiteSpace(result.Value));
                }

                continue;
            }

            var task = tasks.FirstOrDefault(t =>
                string.Equals(t.Name, result.Name, StringComparison.OrdinalIgnoreCase));

            if (task == null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Post-session task result skipped: no matching task definition for '{Name}'.",
                        result.Name);
                }

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
                Status = PostSessionTaskResultStatus.Succeeded,
                ProcessedAtUtc = now,
            };

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Post-session task '{TaskName}' applied successfully (ValueLength={ValueLength}).",
                    task.Name,
                    result.Value.Length);
            }
        }

        return applied;
    }

    private async Task<IChatClient> GetChatClientAsync(AIProfile profile)
    {
        if (_deploymentManager != null)
        {
            var deployment = await _deploymentManager.ResolveUtilityOrDefaultAsync(
                utilityDeploymentName: profile.UtilityDeploymentName,
                chatDeploymentName: profile.ChatDeploymentName);

            if (deployment != null && !string.IsNullOrEmpty(deployment.ConnectionName) && !string.IsNullOrEmpty(deployment.ModelName))
            {
                return await _clientFactory.CreateChatClientAsync(deployment.ClientName, deployment.ConnectionName, deployment.ModelName);
            }
        }

        return null;
    }

    private async Task<IList<AITool>> ResolveToolsAsync(string sessionId, string[] toolNames)
    {
        if (toolNames is null || toolNames.Length == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "No tool names configured for post-session processing of session '{SessionId}'.",
                    sessionId);
            }

            return null;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Resolving {ToolCount} tool(s) for post-session processing of session '{SessionId}': [{ToolNames}].",
                toolNames.Length,
                sessionId,
                string.Join(", ", toolNames));
        }

        var tools = new List<AITool>();

        foreach (var name in toolNames)
        {
            var tool = await _toolsService.GetByNameAsync(name);

            if (tool is not null)
            {
                tools.Add(tool);
            }
            else
            {
                _logger.LogWarning(
                    "Post-session tool '{ToolName}' could not be resolved for session '{SessionId}'. Ensure the tool is registered and its feature is enabled.",
                    name,
                    sessionId);
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
