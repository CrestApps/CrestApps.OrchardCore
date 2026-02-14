using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class FunctionInvocationAICompletionServiceHandler : IAICompletionServiceHandler
{
    /// <summary>
    /// Key used to store scoped <see cref="ToolRegistryEntry"/> instances in
    /// <see cref="AICompletionContext.AdditionalProperties"/> so the handler can
    /// resolve tools from their factories without a second registry lookup.
    /// </summary>
    public const string ScopedEntriesKey = "_scopedToolEntries";

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    public FunctionInvocationAICompletionServiceHandler(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<FunctionInvocationAICompletionServiceHandler> logger)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task ConfigureAsync(CompletionServiceConfigureContext context)
    {
        if (!context.IsFunctionInvocationSupported ||
            context.CompletionContext is null ||
            !context.CompletionContext.AdditionalProperties.TryGetValue(ScopedEntriesKey, out var entriesObj) ||
            entriesObj is not IReadOnlyList<ToolRegistryEntry> scopedEntries ||
            scopedEntries.Count == 0)
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        var user = _httpContextAccessor.HttpContext?.User;
        var services = _httpContextAccessor.HttpContext?.RequestServices;
        var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Process entries in priority order: Local/System first, then MCP.
        // This ensures local tools win name collisions over MCP tools.
        var orderedEntries = scopedEntries
            .OrderBy(e => e.Source == ToolRegistryEntrySource.McpServer ? 1 : 0);

        foreach (var entry in orderedEntries)
        {
            if (entry.Source == ToolRegistryEntrySource.Local &&
                !await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, entry.Name as object))
            {
                continue;
            }

            if (entry.ToolFactory is null)
            {
                _logger.LogWarning("Tool entry '{ToolName}' ({Id}) has no ToolFactory. Skipping.", entry.Name, entry.Id);

                continue;
            }

            // Skip duplicate function names.
            if (!addedNames.Add(entry.Name))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Skipping tool '{ToolName}' from {Source} ({Id}) — name already registered.",
                        entry.Name, entry.Source, entry.Id);
                }

                continue;
            }

            try
            {
                var tool = await entry.ToolFactory(services);

                if (tool is not null)
                {
                    context.ChatOptions.Tools.Add(tool);
                }
                else if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("ToolFactory returned null for '{ToolName}' ({Id}).", entry.Name, entry.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create tool '{ToolName}' ({Id}). Skipping.", entry.Name, entry.Id);
            }
        }
    }
}
