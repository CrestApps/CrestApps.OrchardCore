using CrestApps.AgentSkills.Mcp.Abstractions;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

public sealed class DefaultMcpServerResourceService : IMcpServerResourceService
{
    private readonly ISourceCatalogManager<McpResource> _catalogManager;
    private readonly IMcpResourceProvider _skillResourceProvider;
    private readonly IEnumerable<McpServerResource> _sdkResources;

    public DefaultMcpServerResourceService(
        ISourceCatalogManager<McpResource> catalogManager,
        IMcpResourceProvider skillResourceProvider,
        IEnumerable<McpServerResource> sdkResources)
    {
        _catalogManager = catalogManager;
        _skillResourceProvider = skillResourceProvider;
        _sdkResources = sdkResources;
    }

    public async Task<IList<Resource>> ListAsync()
    {
        var allResources = await GetAllResourcesAsync();

        // Only return concrete resources (URIs without template parameters).
        return allResources
            .Where(r => r.Uri is null || !r.Uri.Contains('{'))
            .ToList();
    }

    public async Task<IList<ResourceTemplate>> ListTemplatesAsync()
    {
        var allResources = await GetAllResourcesAsync();

        // Return resources with template parameters as ResourceTemplate objects.
        var templates = new List<ResourceTemplate>();

        foreach (var resource in allResources)
        {
            if (resource.Uri is not null && resource.Uri.Contains('{'))
            {
                templates.Add(new ResourceTemplate
                {
                    Name = resource.Name,
                    UriTemplate = resource.Uri,
                    Description = resource.Description,
                    MimeType = resource.MimeType,
                });
            }
        }

        return templates;
    }

    private async Task<IList<Resource>> GetAllResourcesAsync()
    {
        var entries = await _catalogManager.GetAllAsync();

        var resources = entries
            .Where(e => e.Resource != null)
            .Select(e => e.Resource)
            .ToList();

        var skillResources = await _skillResourceProvider.GetResourcesAsync();

        foreach (var skillResource in skillResources)
        {
            if (skillResource.ProtocolResource is not null)
            {
                resources.Add(skillResource.ProtocolResource);
            }
        }

        // Include resources registered via the MCP C# SDK.
        foreach (var sdkResource in _sdkResources)
        {
            if (sdkResource.ProtocolResource is not null &&
                !resources.Any(r => r.Uri == sdkResource.ProtocolResource.Uri))
            {
                resources.Add(sdkResource.ProtocolResource);
            }
        }

        return resources;
    }

    public async Task<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken)
    {
        // Check file system skill resources first.
        var skillResources = await _skillResourceProvider.GetResourcesAsync();
        var matchedSkillResource = skillResources.FirstOrDefault(r => r.IsMatch(request.Params.Uri));

        if (matchedSkillResource is not null)
        {
            return await matchedSkillResource.ReadAsync(request, cancellationToken);
        }

        // Try resources registered via the MCP C# SDK.
        var sdkResource = _sdkResources.FirstOrDefault(r => r.IsMatch(request.Params.Uri));

        if (sdkResource is not null)
        {
            return await sdkResource.ReadAsync(request, cancellationToken);
        }

        // Parse the URI: {scheme}://{itemId}/{path}
        if (!McpResourceUri.TryParse(request.Params.Uri, out var resourceUri))
        {
            throw new McpException($"Invalid URI format: '{request.Params.Uri}'.");
        }

        if (string.IsNullOrEmpty(resourceUri.ItemId))
        {
            throw new McpException($"Resource URI '{request.Params.Uri}' does not contain a valid ItemId.");
        }

        var entry = await _catalogManager.FindByIdAsync(resourceUri.ItemId);

        if (entry?.Resource is null)
        {
            throw new McpException($"Resource '{request.Params.Uri}' not found.");
        }

        // Get the appropriate type handler for this resource using keyed services.
        var handler = request.Services.GetKeyedService<IMcpResourceTypeHandler>(entry.Source);

        if (handler is null)
        {
            throw new McpException($"No handler found for resource type '{entry.Source}'.");
        }

        return await handler.ReadAsync(entry, resourceUri, cancellationToken);
    }
}
