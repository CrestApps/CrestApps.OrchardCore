using System.Runtime.CompilerServices;
using System.Text;
using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using GitHub.Copilot.SDK;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

/// <summary>
/// An orchestrator that delegates planning, tool selection, and execution to the
/// GitHub Copilot SDK. Copilot handles all agentic behavior including tool invocation,
/// while this orchestrator bridges the OrchardCore tool registry and MCP connections
/// into the Copilot session.
/// </summary>
public sealed class CopilotOrchestrator : IOrchestrator
{
    public const string OrchestratorName = "copilot";

    private readonly IToolRegistry _toolRegistry;
    private readonly GitHubOAuthService _oauthService;
    private readonly UserManager<IUser> _userManager;
    private readonly ILogger _logger;

    public CopilotOrchestrator(
        IToolRegistry toolRegistry,
        GitHubOAuthService oauthService,
        UserManager<IUser> userManager,
        ILogger<CopilotOrchestrator> logger)
    {
        _toolRegistry = toolRegistry;
        _oauthService = oauthService;
        _userManager = userManager;
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

        // Store scoped entries so the FunctionInvocationHandler can resolve tool factories.
        context.CompletionContext.ToolNames = allTools.Select(e => e.Name).ToArray();
        context.CompletionContext.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = allTools;

        // Build AIFunction instances from non-MCP registry entries only.
        // MCP tools are excluded because Copilot manages MCP connections natively.
        var tools = new List<AIFunction>();

        foreach (var entry in allTools)
        {
            if (entry.Source == ToolRegistryEntrySource.McpServer || entry.ToolFactory is null)
            {
                continue;
            }

            try
            {
                var aiTool = context.ServiceProvider is not null
                    ? await entry.ToolFactory(context.ServiceProvider)
                    : null;

                if (aiTool is AIFunction function)
                {
                    tools.Add(function);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create tool '{ToolName}' from registry entry.", entry.Name);
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "CopilotOrchestrator: Resolved {ToolCount} tool(s): [{Tools}]",
                tools.Count, string.Join(", ", tools.Select(t => t.Name)));
        }

        // Build the session configuration.
        var sessionConfig = new SessionConfig
        {
            Streaming = true,
        };

        // Read Copilot-specific metadata from the orchestration context.
        CopilotSessionMetadata metadata = null;

        if (context.Properties.TryGetValue(nameof(CopilotSessionMetadata), out var metadataObj)
            && metadataObj is CopilotSessionMetadata md)
        {
            metadata = md;
            sessionConfig.Model = metadata.CopilotModel;
        }

        if (tools.Count > 0)
        {
            sessionConfig.Tools = tools;
        }

        // The system message is fully built by the orchestration context handler pipeline
        // (including any RAG/document context). Keep it clean (system instructions only).
        var systemMessage = context.CompletionContext.SystemMessage;

        if (!string.IsNullOrWhiteSpace(systemMessage))
        {
            sessionConfig.SystemMessage = new SystemMessageConfig
            {
                Content = systemMessage,
            };
        }

        // Configure MCP servers so Copilot can manage MCP tools natively.
        await ConfigureMcpServersAsync(context, sessionConfig, cancellationToken);

        // Build client options with authentication and CLI flags.
        var clientOptions = BuildClientOptions(context, metadata);

        string responseText;

        try
        {
            responseText = await RunCopilotSessionAsync(clientOptions, sessionConfig, context, cancellationToken);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "CopilotOrchestrator: CLI process error. The Copilot CLI may have crashed or failed to start.");
            responseText = "The Copilot service encountered an error and could not process your request. Please try again.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "CopilotOrchestrator: Unexpected error during Copilot session.");
            responseText = "An unexpected error occurred while communicating with Copilot. Please try again.";
        }

        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent(responseText)],
        };
    }

    /// <summary>
    /// Builds client options from the metadata and context, using the SDK's
    /// <c>GithubToken</c> property for authentication and <c>CliArgs</c> for
    /// Copilot execution flags.
    /// </summary>
    private CopilotClientOptions BuildClientOptions(
        OrchestrationContext context,
        CopilotSessionMetadata metadata)
    {
        var clientOptions = new CopilotClientOptions();
        string accessToken = null;

        // First, try profile-level credentials from the metadata.
        if (metadata is not null && !string.IsNullOrEmpty(metadata.ProtectedAccessToken))
        {
            accessToken = _oauthService.UnprotectAccessToken(metadata);

            if (!string.IsNullOrEmpty(accessToken) && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "CopilotOrchestrator: Using profile-level credential from {Username}",
                    metadata.GitHubUsername);
            }
        }

        // Fall back to the current user's credentials.
        if (string.IsNullOrEmpty(accessToken) && context.ServiceProvider is not null)
        {
            var httpContextAccessor = context.ServiceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var user = httpContextAccessor?.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var orchardUser = _userManager.GetUserAsync(user).GetAwaiter().GetResult();

                if (orchardUser is not null)
                {
                    var userId = _userManager.GetUserIdAsync(orchardUser).GetAwaiter().GetResult();
                    accessToken = _oauthService.GetValidAccessTokenAsync(userId).GetAwaiter().GetResult();

                    if (!string.IsNullOrEmpty(accessToken) && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "CopilotOrchestrator: Using user-level credential for user {UserId}",
                            userId);
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(accessToken))
        {
            // Use the SDK's GithubToken property for authentication.
            // This sets UseLoggedInUser = false automatically.
            clientOptions.GithubToken = accessToken;
        }
        else
        {
            _logger.LogWarning(
                "CopilotOrchestrator: No valid GitHub access token found. " +
                "The session may fail without authentication.");
        }

        // Pass the --allow-all flag as a CLI argument when enabled.
        if (metadata is not null && metadata.IsAllowAll)
        {
            clientOptions.CliArgs = ["--allow-all"];
        }

        return clientOptions;
    }

    /// <summary>
    /// Runs a Copilot session and returns the complete response text.
    /// Isolated into its own method so the caller can wrap with error handling.
    /// </summary>
    private async Task<string> RunCopilotSessionAsync(
        CopilotClientOptions clientOptions,
        SessionConfig sessionConfig,
        OrchestrationContext context,
        CancellationToken cancellationToken)
    {
        await using var client = new CopilotClient(clientOptions);
        await using var session = await client.CreateSessionAsync(sessionConfig, cancellationToken);

        var responseBuilder = new StringBuilder();
        var completionSource = new TaskCompletionSource<bool>();
        var hasError = false;

        using var subscription = session.On(ev =>
        {
            if (ev is AssistantMessageDeltaEvent deltaEvent)
            {
                responseBuilder.Append(deltaEvent.Data.DeltaContent);
            }
            else if (ev is SessionIdleEvent)
            {
                completionSource.TrySetResult(true);
            }
            else if (ev is SessionErrorEvent errorEvent)
            {
                _logger.LogError(
                    "CopilotOrchestrator: Session error - {ErrorType}: {Message}",
                    errorEvent.Data?.ErrorType, errorEvent.Data?.Message);
                hasError = true;
                completionSource.TrySetResult(false);
            }
        });

        // The Copilot SDK accepts a single prompt string per SendAsync call.
        // Since each request creates a new stateless session, include conversation
        // history in the prompt so Copilot has multi-turn awareness.
        var prompt = BuildPromptWithHistory(context);

        await session.SendAsync(new MessageOptions
        {
            Prompt = prompt,
        }, cancellationToken);

        await completionSource.Task.WaitAsync(cancellationToken);

        var responseText = responseBuilder.ToString();

        if (string.IsNullOrWhiteSpace(responseText) && !hasError)
        {
            responseText = "AI drew blank and no message was generated!";
        }

        return responseText;
    }

    /// <summary>
    /// Combines conversation history with the current user message so Copilot
    /// has full multi-turn context within a single stateless session.
    /// </summary>
    private static string BuildPromptWithHistory(OrchestrationContext context)
    {
        if (context.ConversationHistory is not { Count: > 0 })
        {
            return context.UserMessage;
        }

        var sb = new StringBuilder();
        sb.AppendLine("[Conversation History]");

        foreach (var message in context.ConversationHistory)
        {
            if (string.IsNullOrEmpty(message.Text))
            {
                continue;
            }

            if (message.Role == ChatRole.User)
            {
                sb.Append("User: ");
            }
            else if (message.Role == ChatRole.Assistant)
            {
                sb.Append("Assistant: ");
            }
            else
            {
                continue;
            }

            sb.AppendLine(message.Text);
        }

        sb.AppendLine();
        sb.AppendLine("[Current Message]");
        sb.Append(context.UserMessage);

        return sb.ToString();
    }

    /// <summary>
    /// Retrieves MCP connection metadata from the connection store and configures
    /// them on the Copilot session so that Copilot can manage MCP tools natively.
    /// </summary>
    private async Task ConfigureMcpServersAsync(
        OrchestrationContext context,
        SessionConfig sessionConfig,
        CancellationToken cancellationToken)
    {
        var mcpConnectionIds = context.CompletionContext.McpConnectionIds;

        if (mcpConnectionIds is not { Length: > 0 })
        {
            return;
        }

        // Resolve the MCP connection store from the service provider.
        // This is optional — if the MCP module isn't enabled, no connections are configured.
        var connectionStore = context.ServiceProvider?.GetService<IReadCatalog<McpConnection>>();

        if (connectionStore is null)
        {
            _logger.LogDebug(
                "CopilotOrchestrator: MCP connection store not available; skipping MCP configuration.");
            return;
        }

        var connections = await connectionStore.GetAsync(mcpConnectionIds);

        if (connections is not { Count: > 0 })
        {
            return;
        }

        // TODO: When the Copilot SDK adds native McpServers support on SessionConfig,
        // configure each connection directly. For now, describe available MCP servers
        // in the system message so Copilot is aware of them.
        var mcpDescription = new StringBuilder();
        mcpDescription.AppendLine();
        mcpDescription.AppendLine("[Available MCP Servers]");

        foreach (var connection in connections)
        {
            mcpDescription.Append("- ");
            mcpDescription.Append(connection.DisplayText ?? connection.ItemId);

            if (connection.Source == McpConstants.TransportTypes.Sse)
            {
                var sseMetadata = connection.As<SseMcpConnectionMetadata>();

                if (sseMetadata?.Endpoint is not null)
                {
                    mcpDescription.Append(" (SSE: ");
                    mcpDescription.Append(sseMetadata.Endpoint);
                    mcpDescription.Append(')');
                }
            }
            else if (connection.Source == McpConstants.TransportTypes.StdIo)
            {
                var stdioMetadata = connection.As<StdioMcpConnectionMetadata>();

                if (!string.IsNullOrEmpty(stdioMetadata?.Command))
                {
                    mcpDescription.Append(" (StdIO: ");
                    mcpDescription.Append(stdioMetadata.Command);
                    mcpDescription.Append(')');
                }
            }

            mcpDescription.AppendLine();
        }

        // Append MCP server information to the system message.
        if (sessionConfig.SystemMessage is not null)
        {
            sessionConfig.SystemMessage.Content += mcpDescription.ToString();
        }
        else
        {
            sessionConfig.SystemMessage = new SystemMessageConfig
            {
                Content = mcpDescription.ToString(),
            };
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "CopilotOrchestrator: Configured {Count} MCP connection(s): [{Connections}]",
                connections.Count,
                string.Join(", ", connections.Select(c => c.DisplayText ?? c.ItemId)));
        }
    }
}
