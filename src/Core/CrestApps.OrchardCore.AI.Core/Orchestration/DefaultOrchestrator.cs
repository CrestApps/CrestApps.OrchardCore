using System.Runtime.CompilerServices;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Models;
using Cysharp.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// The default orchestrator implementation that uses a planning-driven approach
/// with scoped tool injection and progressive tool expansion.
/// </summary>
/// <remarks>
/// <para>When the number of available tools exceeds the <see cref="DefaultOrchestratorOptions.PlanningThreshold"/>,
/// the orchestrator runs a lightweight planning phase to identify required capabilities,
/// then scopes the tool set to only the most relevant tools.</para>
/// <para>For small tool sets (below the threshold), all configured tools are injected directly
/// without planning overhead, matching legacy behavior.</para>
/// </remarks>
public sealed class DefaultOrchestrator : IOrchestrator
{
    public const string OrchestratorName = "default";

    private readonly IAICompletionService _completionService;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IAITemplateService _aiTemplateService;
    private readonly AIProviderOptions _providerOptions;
    private readonly IToolRegistry _toolRegistry;
    private readonly ITextTokenizer _tokenizer;
    private readonly DefaultOrchestratorOptions _options;
    private readonly ILogger _logger;

    public DefaultOrchestrator(
        IAICompletionService completionService,
        IAIClientFactory aiClientFactory,
        IAITemplateService aiTemplateService,
        IOptions<AIProviderOptions> providerOptions,
        IToolRegistry toolRegistry,
        ITextTokenizer tokenizer,
        IOptions<DefaultOrchestratorOptions> options,
        ILogger<DefaultOrchestrator> logger)
    {
        _completionService = completionService;
        _aiClientFactory = aiClientFactory;
        _aiTemplateService = aiTemplateService;
        _providerOptions = providerOptions.Value;
        _toolRegistry = toolRegistry;
        _tokenizer = tokenizer;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => OrchestratorName;

    public async IAsyncEnumerable<ChatResponseUpdate> ExecuteStreamingAsync(
        OrchestrationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.CompletionContext);
        ArgumentException.ThrowIfNullOrEmpty(context.SourceName);

        // Get the full tool registry for this context.
        var allTools = await _toolRegistry.GetAllAsync(context.CompletionContext, cancellationToken);

        // Determine the total configured tool count.
        var profileToolCount = allTools.Count;

        IReadOnlyList<ToolRegistryEntry> scopedEntries;

        if (profileToolCount <= _options.ScopingThreshold)
        {
            // Few tools: inject all directly (no scoping or planning overhead).
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Tool count ({ToolCount}) is within scoping threshold ({Threshold}). Passing all tools.",
                    profileToolCount, _options.ScopingThreshold);
            }

            scopedEntries = allTools;
        }
        else
        {
            var hasMcpTools = allTools.Any(t => t.Source == ToolRegistryEntrySource.McpServer);

            if (hasMcpTools || profileToolCount > _options.PlanningThreshold)
            {
                // MCP tools present or very many tools: full LLM planning + scoping.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Tool count ({ToolCount}) with MCP={HasMcp}. Running planning phase.",
                        profileToolCount, hasMcpTools);
                }

                var plan = await PlanAsync(context, allTools, cancellationToken);
                scopedEntries = await ScopeToolsAsync(plan, context, allTools);

                // Add the plan as additional system context for the execution phase.
                if (!string.IsNullOrWhiteSpace(plan))
                {
                    context.CompletionContext.SystemMessage =
                        (context.CompletionContext.SystemMessage ?? string.Empty) +
                        "\n\n[Execution Plan]\n" + plan;
                }
            }
            else
            {
                // Medium tool count, no MCP: lightweight relevance scoping without LLM call.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Tool count ({ToolCount}) exceeds scoping threshold ({Threshold}). Scoping by relevance (no planner).",
                        profileToolCount, _options.ScopingThreshold);
                }

                scopedEntries = await ScopeToolsAsync(null, context, allTools);
            }
        }

        // Derive ToolNames from entries (for logging/diagnostics) and store the
        // entries directly so the handler resolves tools via ToolFactory delegates.
        context.CompletionContext.ToolNames = scopedEntries.Select(e => e.Name).ToArray();
        context.CompletionContext.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = scopedEntries;

        // Execute the completion with the scoped tool set.
        await foreach (var chunk in _completionService.CompleteStreamingAsync(
            context.SourceName, context.ConversationHistory, context.CompletionContext, cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Runs the planning phase: a lightweight LLM call to identify required capabilities.
    /// Uses the utility model when configured, falling back to the default deployment.
    /// </summary>
    internal async Task<string> PlanAsync(
        OrchestrationContext context,
        IReadOnlyList<ToolRegistryEntry> availableTools,
        CancellationToken cancellationToken)
    {
        try
        {
            var userSelectedSummary = BuildToolSummary(
                availableTools.Where(t => t.Source == ToolRegistryEntrySource.Local));
            var systemToolSummary = BuildToolSummary(
                availableTools.Where(t => t.Source == ToolRegistryEntrySource.System));
            var mcpToolSummary = BuildToolSummary(
                availableTools.Where(t => t.Source == ToolRegistryEntrySource.McpServer));

            var arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["userTools"] = userSelectedSummary,
                ["systemTools"] = systemToolSummary,
                ["mcpTools"] = mcpToolSummary,
            };

            var planningSystemPrompt = await _aiTemplateService.RenderAsync(AITemplateIds.TaskPlanning, arguments);

            var chatClient = await TryCreateUtilityChatClientAsync(context);

            string plan;

            if (chatClient != null)
            {
                // Use the utility model directly for the planning call.
                var messages = GetPlanningMessages(context);
                messages.Insert(0, new ChatMessage(ChatRole.System, planningSystemPrompt));

                var chatOptions = new ChatOptions
                {
                    Temperature = 0.1f,
                    MaxOutputTokens = 300,
                };

                var response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                plan = response?.Text;
            }
            else
            {
                // Fall back to the completion service with the default model.
                var planningContext = new AICompletionContext
                {
                    ConnectionName = context.CompletionContext.ConnectionName,
                    DeploymentId = context.CompletionContext.DeploymentId,
                    DisableTools = true,
                    SystemMessage = planningSystemPrompt,
                    Temperature = 0.1f,
                    MaxTokens = 300,
                    UseCaching = false,
                };

                var response = await _completionService.CompleteAsync(
                    context.SourceName,
                    GetPlanningMessages(context),
                    planningContext,
                    cancellationToken);

                plan = response?.Messages?.FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Text;
            }

            if (_logger.IsEnabled(LogLevel.Debug) && !string.IsNullOrWhiteSpace(plan))
            {
                _logger.LogDebug("Planning phase output: {Plan}", plan);
            }

            return plan;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planning phase failed. Falling back to full tool injection.");

            return null;
        }
    }

    /// <summary>
    /// Scopes tools by matching a scoring text against the tool registry
    /// using the shared <see cref="ITextTokenizer"/> for consistent tokenization.
    /// Returns the actual <see cref="ToolRegistryEntry"/> instances so they flow
    /// directly to the handler without a lossy name-based round-trip.
    /// </summary>
    /// <remarks>
    /// <para>When a plan is provided (from the LLM planning phase), the plan text is used
    /// for scoring. When no plan is available (lightweight scoping mode), the user's
    /// message and recent conversation context are used instead.</para>
    /// <para>All tools (local, system, and MCP) are scored uniformly by relevance and
    /// the top-N are selected based on the configured budget. This ensures only the
    /// most relevant tools are included regardless of their source.</para>
    /// </remarks>
    internal Task<IReadOnlyList<ToolRegistryEntry>> ScopeToolsAsync(
        string plan,
        OrchestrationContext context,
        IReadOnlyList<ToolRegistryEntry> allTools)
    {
        // All tools are subject to relevance scoring when the total count
        // exceeds the scoping threshold. No source gets special treatment.
        var budget = _options.InitialToolCount;

        // Determine the text to score against: plan text if available,
        // otherwise fall back to user message + recent conversation context.
        var scoringText = !string.IsNullOrWhiteSpace(plan)
            ? plan
            : BuildScoringContext(context);

        if (string.IsNullOrWhiteSpace(scoringText))
        {
            // No scoring text available; return capped tools by original order.
            return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(
                allTools.Take(Math.Max(budget, _options.MaxToolCount)).ToList());
        }

        var scoringTokens = _tokenizer.Tokenize(scoringText);

        if (scoringTokens.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(
                allTools.Take(budget).ToList());
        }

        // Score all tools uniformly by relevance.
        var scored = new List<(ToolRegistryEntry Entry, double Score)>();

        foreach (var tool in allTools)
        {
            var title = tool.Name;

            if (!string.IsNullOrWhiteSpace(tool.Description))
            {
                title += ' ' + tool.Description;
            }

            var toolTokens = _tokenizer.Tokenize(title);

            if (toolTokens.Count == 0)
            {
                scored.Add((tool, 0));
                continue;
            }

            var matchCount = 0;

            foreach (var scoringToken in scoringTokens)
            {
                if (toolTokens.Contains(scoringToken))
                {
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                scored.Add((tool, 0));
                continue;
            }

            // Use max of forward and reverse ratios for better recall.
            var forwardScore = (double)matchCount / scoringTokens.Count;
            var reverseScore = (double)matchCount / toolTokens.Count;
            scored.Add((tool, Math.Max(forwardScore, reverseScore)));
        }

        var scopedEntries = scored
            .Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score)
            .Take(budget)
            .Select(s => s.Entry)
            .ToList();

        // If no tools matched, fill budget by original order as fallback.
        if (scopedEntries.Count == 0 && budget > 0)
        {
            scopedEntries = allTools
                .Take(budget)
                .ToList();
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Tool scoping selected {Count} tool(s) from {Total}: [{Tools}]",
                scopedEntries.Count, allTools.Count,
                string.Join(", ", scopedEntries.Select(e => e.Name)));
        }

        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(scopedEntries);
    }

    /// <summary>
    /// Builds the message list for the planning phase by including recent conversation
    /// history (user and assistant text messages only) so the planner understands
    /// follow-up requests like "yes", "do it", or "also send a notification".
    /// </summary>
    private List<ChatMessage> GetPlanningMessages(OrchestrationContext context)
    {
        var messages = new List<ChatMessage>();

        if (context.ConversationHistory is { Count: > 0 })
        {
            // Take only user/assistant text messages from recent history.
            // Exclude tool call details to keep the planning call lightweight.
            var recentMessages = context.ConversationHistory
                .Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)
                .Where(m => !string.IsNullOrEmpty(m.Text))
                .TakeLast(_options.PlanningHistoryMessageCount);

            messages.AddRange(recentMessages);
        }

        // Ensure the current user message is always included as the last message.
        if (messages.Count == 0 || messages[^1].Text != context.UserMessage)
        {
            messages.Add(new ChatMessage(ChatRole.User, context.UserMessage));
        }

        return messages;
    }

    /// <summary>
    /// Builds a scoring context from the user's current message and recent conversation
    /// history for lightweight token-based tool scoping (no LLM call).
    /// </summary>
    private static string BuildScoringContext(OrchestrationContext context)
    {
        using var sb = ZString.CreateStringBuilder();

        // Include the last assistant reply for context (e.g., "I created article X" → "yes" makes sense).
        if (context.ConversationHistory is { Count: > 0 })
        {
            var lastAssistantMessage = context.ConversationHistory
                .LastOrDefault(m => m.Role == ChatRole.Assistant && !string.IsNullOrEmpty(m.Text));

            if (lastAssistantMessage is not null)
            {
                sb.AppendLine(lastAssistantMessage.Text);
            }
        }

        sb.Append(context.UserMessage);

        return sb.ToString();
    }

    private static string BuildToolSummary(IEnumerable<ToolRegistryEntry> tools)
    {
        using var sb = ZString.CreateStringBuilder();

        foreach (var tool in tools)
        {
            sb.Append("- ");
            sb.Append(tool.Name);

            if (!string.IsNullOrEmpty(tool.Description))
            {
                sb.Append(": ");
                sb.Append(tool.Description);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Attempts to create a chat client using the utility deployment name.
    /// Returns <c>null</c> if no utility or default deployment is configured.
    /// </summary>
    private async Task<IChatClient> TryCreateUtilityChatClientAsync(OrchestrationContext context)
    {
        var providerName = context.SourceName;
        var connectionName = context.CompletionContext?.ConnectionName;

        if (string.IsNullOrEmpty(providerName) ||
            !_providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            return null;
        }

        if (string.IsNullOrEmpty(connectionName))
        {
            connectionName = provider.DefaultConnectionName;
        }

        if (string.IsNullOrEmpty(connectionName) ||
            !provider.Connections.TryGetValue(connectionName, out var connection))
        {
            return null;
        }

        // Prefer the utility deployment, fall back to the default deployment.
        var deploymentName = connection.GetUtilityDeploymentOrDefaultName(throwException: false);

        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = connection.GetChatDeploymentOrDefaultName(throwException: false);
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            return null;
        }

        return await _aiClientFactory.CreateChatClientAsync(providerName, connectionName, deploymentName);
    }
}
