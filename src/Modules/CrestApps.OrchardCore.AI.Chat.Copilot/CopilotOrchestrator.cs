using System.Runtime.CompilerServices;
using System.Text;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
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

namespace CrestApps.OrchardCore.AI.Chat.Copilot;

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
    private readonly IGitHubOAuthService _oauthService;
    private readonly UserManager<IUser> _userManager;
    private readonly ILogger _logger;

    public CopilotOrchestrator(
        IToolRegistry toolRegistry,
        IGitHubOAuthService oauthService,
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

        // The model is configured per-profile or per-interaction. Prefer the explicit
        // Model property (set by CopilotCompletionContextBuilderHandler) over DeploymentId.
        var model = !string.IsNullOrEmpty(context.CompletionContext.Model)
            ? context.CompletionContext.Model
            : context.CompletionContext.DeploymentId;

        // Build the session configuration.
        var sessionConfig = new SessionConfig
        {
            Model = model,
            Streaming = true,
        };

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

        // Get the GitHub access token for the current user
        var clientOptions = new CopilotClientOptions();

        if (context.ServiceProvider is not null)
        {
            var httpContextAccessor = context.ServiceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var user = httpContextAccessor?.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var orchardUser = await _userManager.GetUserAsync(user);

                if (orchardUser is not null)
                {
                    var userId = await _userManager.GetUserIdAsync(orchardUser);
                    var accessToken = await _oauthService.GetValidAccessTokenAsync(userId, cancellationToken);

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        // Pass the GitHub access token via environment variable
                        // The Copilot CLI uses GITHUB_TOKEN for authentication
                        clientOptions.Environment = new Dictionary<string, string>
                        {
                            ["GITHUB_TOKEN"] = accessToken
                        };

                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug(
                                "CopilotOrchestrator: Configured GitHub access token for user {UserId}",
                                userId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "CopilotOrchestrator: No valid GitHub access token found for user {UserId}. " +
                            "User must authenticate with GitHub via AI Profile settings.",
                            userId);
                    }
                }
            }
        }

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

        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent(responseText)],
        };
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
                var metadata = connection.As<SseMcpConnectionMetadata>();

                if (metadata?.Endpoint is not null)
                {
                    mcpDescription.Append(" (SSE: ");
                    mcpDescription.Append(metadata.Endpoint);
                    mcpDescription.Append(')');
                }
            }
            else if (connection.Source == McpConstants.TransportTypes.StdIo)
            {
                var metadata = connection.As<StdioMcpConnectionMetadata>();

                if (!string.IsNullOrEmpty(metadata?.Command))
                {
                    mcpDescription.Append(" (StdIO: ");
                    mcpDescription.Append(metadata.Command);
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
