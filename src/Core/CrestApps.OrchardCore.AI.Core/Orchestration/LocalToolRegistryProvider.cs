using CrestApps.OrchardCore.AI.Models;
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

    public LocalToolRegistryProvider(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _toolDefinitions = toolDefinitions;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

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
                ToolFactory = (sp) => ValueTask.FromResult(sp.GetKeyedService<AITool>(name)),
            });
        }

        return entries;
    }
}
