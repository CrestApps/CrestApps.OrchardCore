using CrestApps.Core.AI.Models;
using CrestApps.Core.Security;
using CrestApps.Core.AI.Tooling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// Provides local tool metadata from <see cref="AIToolDefinitionOptions"/> to the tool registry.
/// </summary>
internal sealed class LocalToolRegistryProvider : IToolRegistryProvider
{
    private readonly IOptions<AIToolDefinitionOptions> _toolDefinitions;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserAccessor _userAccessor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalToolRegistryProvider"/> class.
    /// </summary>
    /// <param name="toolDefinitions">The registered AI tool definitions.</param>
    /// <param name="authorizationService">The authorization service for verifying tool access.</param>
    /// <param name="userAccessor">The accessor used to resolve the caller that the tools are being resolved for.</param>
    /// <param name="logger">The logger.</param>
    public LocalToolRegistryProvider(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAuthorizationService authorizationService,
        IUserAccessor userAccessor,
        ILogger<LocalToolRegistryProvider> logger)
    {
        _toolDefinitions = toolDefinitions;
        _authorizationService = authorizationService;
        _userAccessor = userAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves locally registered tool entries that are configured on the given completion context
    /// and authorized for the current user.
    /// </summary>
    /// <param name="context">The AI completion context specifying the requested tool names.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of authorized <see cref="ToolRegistryEntry"/> instances.</returns>
    public async Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var configuredToolNames = context?.ToolNames;

        if (configuredToolNames is null || configuredToolNames.Length == 0)
        {
            return [];
        }

        var toolDefinitions = _toolDefinitions.Value.Tools;
        var entries = new List<ToolRegistryEntry>();
        var user = _userAccessor.User;
        List<string> unauthorizedToolNames = null;

        foreach (var toolName in configuredToolNames)
        {
            if (!toolDefinitions.TryGetValue(toolName, out var definition))
            {
                continue;
            }

            // Skip system tools — they are provided by SystemToolRegistryProvider.

            if (definition.IsSystemTool)
            {
                continue;
            }

            // A null user means there is no caller at all, such as a background task or a recipe,
            // so authorization is skipped. Unauthenticated callers are still evaluated so that
            // permissions granted to the Anonymous role continue to apply.

            if (user is not null &&
                !await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, toolName as object))
            {
                (unauthorizedToolNames ??= []).Add(toolName);

                continue;
            }

            var name = toolName;

            entries.Add(new ToolRegistryEntry
            {
                Id = name,
                Name = name,
                Description = definition.Description ?? definition.Title ?? name,
                Source = ToolRegistryEntrySource.Local,
                CreateAsync = (sp) => ValueTask.FromResult(sp.GetKeyedService<AITool>(name)),
            });
        }

        if (unauthorizedToolNames is not null)
        {
            _logger.LogWarning("The current user is not authorized to use the following AI tools, which were excluded from the request: {ToolNames}. Grant the 'AccessAnyAITool' permission, or the matching per-tool 'AccessAITool_<tool name>' permission, to the roles that require them.", string.Join(", ", unauthorizedToolNames));
        }

        return entries;
    }
}
