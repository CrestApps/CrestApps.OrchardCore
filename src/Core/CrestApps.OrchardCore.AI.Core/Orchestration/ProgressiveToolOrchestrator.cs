using System.Runtime.CompilerServices;
using System.Text;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// The default orchestrator implementation that uses a planning-driven approach
/// with scoped tool injection and progressive tool expansion.
/// </summary>
/// <remarks>
/// <para>When the number of available tools exceeds the <see cref="ProgressiveToolOrchestratorOptions.PlanningThreshold"/>,
/// the orchestrator runs a lightweight planning phase to identify required capabilities,
/// then scopes the tool set to only the most relevant tools.</para>
/// <para>For small tool sets (below the threshold), all configured tools are injected directly
/// without planning overhead, matching legacy behavior.</para>
/// </remarks>
public sealed class ProgressiveToolOrchestrator : IOrchestrator
{
    public const string OrchestratorName = "default";

    private readonly IAICompletionService _completionService;
    private readonly IToolRegistry _toolRegistry;
    private readonly ITextTokenizer _tokenizer;
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly ProgressiveToolOrchestratorOptions _options;
    private readonly ILogger _logger;

    public ProgressiveToolOrchestrator(
        IAICompletionService completionService,
        IToolRegistry toolRegistry,
        ITextTokenizer tokenizer,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IOptions<ProgressiveToolOrchestratorOptions> options,
        ILogger<ProgressiveToolOrchestrator> logger)
    {
        _completionService = completionService;
        _toolRegistry = toolRegistry;
        _tokenizer = tokenizer;
        _toolDefinitions = toolDefinitions.Value;
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

        // Enrich system message with document metadata if documents are available.
        EnrichSystemMessageWithDocuments(context);

        // Get the full tool registry for this context.
        var allTools = await _toolRegistry.GetAllAsync(context.CompletionContext, cancellationToken);

        // Determine the total configured tool count.
        var profileToolCount = allTools.Count;

        if (profileToolCount <= _options.ScopingThreshold)
        {
            // Few tools: inject all directly (no scoping or planning overhead).
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Tool count ({ToolCount}) is within scoping threshold ({Threshold}). Passing all tools.",
                    profileToolCount, _options.ScopingThreshold);
            }

            context.CompletionContext.ToolNames = allTools.Select(t => t.Name).ToArray();
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
                var scopedTools = await ScopeToolsAsync(plan, context, allTools, cancellationToken);

                context.CompletionContext.ToolNames = scopedTools;

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

                var scopedTools = await ScopeToolsAsync(null, context, allTools, cancellationToken);
                context.CompletionContext.ToolNames = scopedTools;
            }
        }

        // Execute the completion with the scoped tool set.
        await foreach (var chunk in _completionService.CompleteStreamingAsync(
            context.SourceName, context.ConversationHistory, context.CompletionContext, cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Runs the planning phase: a lightweight LLM call to identify required capabilities.
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
            var otherToolSummary = BuildToolSummary(
                availableTools.Where(t => t.Source != ToolRegistryEntrySource.Local));

            var planningSystemPrompt = $"""
                You are a task planner. Analyze the user's request and identify what capabilities/tools are needed to fulfill it.

                The following tools were explicitly selected by the user and are always available:
                {userSelectedSummary}

                Additional capabilities that may be relevant:
                {otherToolSummary}

                Respond with a brief plan listing the required steps and which capabilities are needed.
                Focus on identifying the NAMES of relevant capabilities from the lists above.
                Prefer using the user-selected tools when they match the request.
                Keep your response concise (under 200 words).
                """;

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

            var plan = response?.Messages?.FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Text;

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
    /// </summary>
    /// <remarks>
    /// <para>When a plan is provided (from the LLM planning phase), the plan text is used
    /// for scoring. When no plan is available (lightweight scoping mode), the user's
    /// message and recent conversation context are used instead.</para>
    /// <para>User-selected local tools are always included in the scoped set because the user
    /// explicitly chose them for this interaction. Only system and MCP tools are scored
    /// and filtered by relevance.</para>
    /// </remarks>
    internal Task<string[]> ScopeToolsAsync(
        string plan,
        OrchestrationContext context,
        IReadOnlyList<ToolRegistryEntry> allTools,
        CancellationToken cancellationToken)
    {
        // User-selected local tools are always preserved — they were explicitly
        // chosen for this interaction and should not be filtered out.
        var localToolNames = allTools
            .Where(t => t.Source == ToolRegistryEntrySource.Local)
            .Select(t => t.Name)
            .ToList();

        var nonLocalTools = allTools
            .Where(t => t.Source != ToolRegistryEntrySource.Local)
            .ToList();

        // Calculate the remaining budget for scored tools.
        var remainingBudget = Math.Max(0, _options.InitialToolCount - localToolNames.Count);

        // Determine the text to score against: plan text if available,
        // otherwise fall back to user message + recent conversation context.
        var scoringText = !string.IsNullOrWhiteSpace(plan)
            ? plan
            : BuildScoringContext(context);

        if (string.IsNullOrWhiteSpace(scoringText))
        {
            // No scoring text available; return local tools + capped non-local tools.
            var fallbackNames = localToolNames.Concat(
                nonLocalTools
                    .Take(Math.Max(remainingBudget, _options.MaxToolCount - localToolNames.Count))
                    .Select(t => t.Name));

            return Task.FromResult(fallbackNames.ToArray());
        }

        var scoringTokens = _tokenizer.Tokenize(scoringText);

        if (scoringTokens.Count == 0)
        {
            var fallbackNames = localToolNames.Concat(
                nonLocalTools
                    .Take(remainingBudget)
                    .Select(t => t.Name));

            return Task.FromResult(fallbackNames.ToArray());
        }

        // Score only non-local tools by relevance.
        var scored = new List<(ToolRegistryEntry Entry, double Score)>();

        foreach (var tool in nonLocalTools)
        {
            var toolTokens = _tokenizer.Tokenize(tool.Name + " " + (tool.Description ?? string.Empty));

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

        var scoredToolNames = scored
            .Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score)
            .Take(remainingBudget)
            .Select(s => s.Entry.Name)
            .ToList();

        // If no non-local tools matched, fill remaining budget by original order.
        if (scoredToolNames.Count == 0 && remainingBudget > 0)
        {
            scoredToolNames = nonLocalTools
                .Take(remainingBudget)
                .Select(t => t.Name)
                .ToList();
        }

        var scopedToolNames = localToolNames.Concat(scoredToolNames).ToArray();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Tool scoping selected {Count} tool(s) from {Total} ({LocalCount} local, {ScoredCount} scored): [{Tools}]",
                scopedToolNames.Length, allTools.Count, localToolNames.Count, scoredToolNames.Count,
                string.Join(", ", scopedToolNames));
        }

        return Task.FromResult(scopedToolNames);
    }

    /// <summary>
    /// Enriches the system message with metadata about available documents so the LLM
    /// knows it can call document system tools. Tool names are discovered dynamically
    /// by querying registered tools with <see cref="AIToolPurposes.DocumentProcessing"/> purpose.
    /// </summary>
    private void EnrichSystemMessageWithDocuments(OrchestrationContext context)
    {
        if (context.Documents is not { Count: > 0 })
        {
            return;
        }

        // Discover document processing tools dynamically by purpose.
        var docTools = _toolDefinitions.Tools
            .Where(t => t.Value.HasPurpose(AIToolPurposes.DocumentProcessing))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("\n\n[Available Documents or attachments]");

        if (docTools.Count > 0)
        {
            sb.AppendLine("The user has uploaded the following documents. Use the available document tools to access their content when needed.");
            sb.AppendLine();
            sb.AppendLine("Available document tools:");

            foreach (var (name, entry) in docTools)
            {
                sb.Append("- ");
                sb.Append(name);
                sb.Append(": ");
                sb.AppendLine(entry.Description ?? entry.Title ?? name);
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("The user has uploaded the following documents.");
        }

        foreach (var doc in context.Documents)
        {
            sb.Append("- ");
            sb.Append(doc.DocumentId);
            sb.Append(": \"");
            sb.Append(doc.FileName);
            sb.Append("\" (");
            sb.Append(doc.ContentType ?? "unknown");
            sb.Append(", ");
            sb.Append(FormatFileSize(doc.FileSize));
            sb.AppendLine(")");
        }

        context.CompletionContext.SystemMessage =
            (context.CompletionContext.SystemMessage ?? string.Empty) + sb.ToString();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):F1} MB";
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
        var sb = new StringBuilder();

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
        var sb = new StringBuilder();

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
}
