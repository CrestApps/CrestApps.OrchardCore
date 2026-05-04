using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// Provides local tool metadata from <see cref="AIToolDefinitionOptions"/> to the tool registry.
/// </summary>
internal sealed class LocalToolRegistryProvider : IToolRegistryProvider
{
    private readonly IOptions<AIToolDefinitionOptions> _toolDefinitions;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalToolRegistryProvider"/> class.
    /// </summary>
    /// <param name="toolDefinitions">The registered AI tool definitions.</param>
    /// <param name="authorizationService">The authorization service for verifying tool access.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for retrieving the current user.</param>
    public LocalToolRegistryProvider(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _toolDefinitions = toolDefinitions;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
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
        var user = _httpContextAccessor.HttpContext?.User;

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

            // Verify user has permission to access this tool.

            if (user is not null &&
                !await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, toolName as object))
            {
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

        return entries;
    }
}
