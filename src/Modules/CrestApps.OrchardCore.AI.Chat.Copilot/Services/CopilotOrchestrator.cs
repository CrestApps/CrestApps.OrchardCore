using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using GitHub.Copilot.SDK;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Settings;
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
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public CopilotOrchestrator(
        IToolRegistry toolRegistry,
        GitHubOAuthService oauthService,
        UserManager<IUser> userManager,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<CopilotOrchestrator> logger)
    {
        _toolRegistry = toolRegistry;
        _oauthService = oauthService;
        _userManager = userManager;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
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
                    // Wrap the function so that arguments.Services is always set when
                    // the Copilot SDK invokes the tool. The SDK does not populate
                    // AIFunctionArguments.Services, but tools rely on it to resolve
                    // scoped services (e.g., IHttpContextAccessor, ISiteService).
                    tools.Add(new ServiceInjectedAIFunction(function, context.ServiceProvider));
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

        // Load site-level settings to determine authentication mode.
        var settings = await _siteService.GetSettingsAsync<CopilotSettings>();

        if (settings.AuthenticationType == CopilotAuthenticationType.ApiKey)
        {
            // BYOK mode — configure provider on the session config.
            // The GitHub token is NOT used in this mode; instead, the API key
            // is passed via SessionConfig.Provider (see ConfigureByokProvider).
            ConfigureByokProvider(sessionConfig, settings);
        }

        // For GitHub OAuth mode, the access token is resolved and set on
        // CopilotClientOptions.GithubToken inside BuildClientOptions below.

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
        await ConfigureMcpServersAsync(context, sessionConfig);

        // Build client options with authentication and CLI flags.
        var clientOptions = BuildClientOptions(context, metadata, settings);

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
    /// <c>GithubToken</c> property for GitHub OAuth or <c>UseLoggedInUser = false</c>
    /// for BYOK authentication, and <c>CliArgs</c> for Copilot execution flags.
    /// </summary>
    private CopilotClientOptions BuildClientOptions(
        OrchestrationContext context,
        CopilotSessionMetadata metadata,
        CopilotSettings settings)
    {
        var clientOptions = new CopilotClientOptions();

        if (settings.AuthenticationType == CopilotAuthenticationType.ApiKey)
        {
            // BYOK mode — no GitHub token needed; provider config is on SessionConfig.
            clientOptions.UseLoggedInUser = false;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("CopilotOrchestrator: Using BYOK (API Key) authentication with provider '{ProviderType}'.", settings.ProviderType);
            }
        }
        else
        {
            // GitHub OAuth mode — resolve access token.
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
                clientOptions.GitHubToken = accessToken;
            }
            else
            {
                _logger.LogWarning(
                    "CopilotOrchestrator: No valid GitHub access token found. " +
                    "The session may fail without authentication.");
            }
        }

        // Pass the --allow-all flag as a CLI argument when enabled.
        if (metadata is not null && metadata.IsAllowAll)
        {
            clientOptions.CliArgs = ["--allow-all"];
        }

        return clientOptions;
    }

    /// <summary>
    /// Configures the BYOK provider on the session config using the site-level settings.
    /// </summary>
    private void ConfigureByokProvider(SessionConfig sessionConfig, CopilotSettings settings)
    {
        // Use model from metadata if set, otherwise fall back to site default.
        if (string.IsNullOrEmpty(sessionConfig.Model))
        {
            sessionConfig.Model = settings.DefaultModel;
        }

        var providerConfig = new ProviderConfig
        {
            Type = settings.ProviderType ?? "openai",
            BaseUrl = settings.BaseUrl,
        };

        // Decrypt and set the API key.
        if (!string.IsNullOrEmpty(settings.ProtectedApiKey))
        {
            try
            {
                var protector = _dataProtectionProvider.CreateProtector("CrestApps.OrchardCore.AI.Chat.Copilot.Settings");
                providerConfig.ApiKey = protector.Unprotect(settings.ProtectedApiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CopilotOrchestrator: Failed to unprotect BYOK API key.");
            }
        }

        if (!string.IsNullOrEmpty(settings.WireApi))
        {
            providerConfig.WireApi = settings.WireApi;
        }

        if (string.Equals(settings.ProviderType, "azure", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(settings.AzureApiVersion))
        {
            providerConfig.Azure = new AzureOptions
            {
                ApiVersion = settings.AzureApiVersion,
            };
        }

        sessionConfig.Provider = providerConfig;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "CopilotOrchestrator: Configured BYOK provider type='{Type}', baseUrl='{BaseUrl}', model='{Model}'.",
                providerConfig.Type, providerConfig.BaseUrl, sessionConfig.Model);
        }
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
        string errorMessage = null;

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
                var data = errorEvent.Data;

                _logger.LogError(
                    "CopilotOrchestrator: Session error - {ErrorType}: {Message} (StatusCode: {StatusCode})",
                    data?.ErrorType, data?.Message, data?.StatusCode);

                errorMessage = BuildErrorMessage(data);
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

        if (!string.IsNullOrEmpty(errorMessage))
        {
            return errorMessage;
        }

        var responseText = responseBuilder.ToString();

        if (string.IsNullOrWhiteSpace(responseText))
        {
            responseText = "AI drew blank and no message was generated!";
        }

        return responseText;
    }

    /// <summary>
    /// Builds a user-facing error message from the Copilot session error data.
    /// </summary>
    private static string BuildErrorMessage(SessionErrorData data)
    {
        if (data is null)
        {
            return "The Copilot service encountered an error. Please try again.";
        }

        var sb = new StringBuilder();

        if (data.StatusCode.HasValue)
        {
            var statusCode = (int)data.StatusCode.Value;

            sb.Append(statusCode switch
            {
                401 => "**Copilot Authentication failed.** The API key may be invalid or expired. Please verify your API key in the Copilot settings.",
                403 => "**Copilot Access denied.** The API key does not have permission to access this resource. Please check your API key permissions.",
                404 => "**Copilot Endpoint not found.** The base URL may be incorrect. Please verify the provider URL in the Copilot settings.",
                429 => "**Copilot Rate limit exceeded.** Too many requests were sent. Please wait a moment and try again.",
                >= 500 => $"**Copilot Provider service error (HTTP {statusCode}).** The model provider is experiencing issues. Please try again later.",
                _ => $"**Copilot Request failed (HTTP {statusCode}).**",
            });
        }
        else
        {
            sb.Append("**Copilot session error.**");
        }

        if (!string.IsNullOrEmpty(data.Message))
        {
            sb.Append(' ');
            sb.Append(data.Message);
        }

        return sb.ToString();
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
        SessionConfig sessionConfig)
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

    /// <summary>
    /// A thin wrapper around an <see cref="AIFunction"/> that ensures
    /// <see cref="AIFunctionArguments.Services"/> is always set before the
    /// inner function is invoked. The Copilot SDK does not populate this
    /// property, but OrchardCore tools depend on it for scoped service resolution.
    /// </summary>
    private sealed class ServiceInjectedAIFunction : AIFunction
    {
        private readonly AIFunction _inner;
        private readonly IServiceProvider _services;

        public ServiceInjectedAIFunction(AIFunction inner, IServiceProvider services)
        {
            _inner = inner;
            _services = services;
        }

        public override string Name => _inner.Name;

        public override string Description => _inner.Description;

        public override JsonElement JsonSchema => _inner.JsonSchema;

        public override JsonElement? ReturnJsonSchema => _inner.ReturnJsonSchema;

        public override IReadOnlyDictionary<string, object> AdditionalProperties
            => _inner.AdditionalProperties;

        public override JsonSerializerOptions JsonSerializerOptions
            => _inner.JsonSerializerOptions;

        public override MethodInfo UnderlyingMethod
            => _inner.UnderlyingMethod;

        protected override ValueTask<object> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            arguments.Services ??= _services;

            return _inner.InvokeAsync(arguments, cancellationToken);
        }
    }
}
