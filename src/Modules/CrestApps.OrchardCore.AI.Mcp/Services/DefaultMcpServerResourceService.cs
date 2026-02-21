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
        var validResources = allResources
            .Where(r => r.Uri is null || !McpResourceUri.IsTemplate(r.Uri))
            .ToList();

        return validResources;
    }

    public async Task<IList<ResourceTemplate>> ListTemplatesAsync()
    {
        var allResources = await GetAllResourcesAsync();

        // Return resources with template parameters as ResourceTemplate objects.
        var templates = new List<ResourceTemplate>();

        foreach (var resource in allResources)
        {
            if (resource.Uri is not null && McpResourceUri.IsTemplate(resource.Uri))
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

        // Parse the URI to extract scheme and itemId for direct lookup.
        // URI format: {source}://{itemId}/{path}
        var uri = request.Params.Uri;
        var schemeEnd = uri.IndexOf("://", StringComparison.Ordinal);

        if (schemeEnd < 0)
        {
            throw new McpException($"Invalid resource URI format: '{uri}'. Expected format: scheme://itemId/path");
        }

        var afterScheme = uri[(schemeEnd + 3)..];
        var slashIndex = afterScheme.IndexOf('/');
        var itemId = slashIndex >= 0 ? afterScheme[..slashIndex] : afterScheme;

        // Look up the resource entry by ItemId.
        var entries = await _catalogManager.GetAllAsync();
        var entry = entries.FirstOrDefault(e => string.Equals(e.ItemId, itemId, StringComparison.OrdinalIgnoreCase));

        if (entry?.Resource?.Uri is null)
        {
            throw new McpException($"Resource '{uri}' not found.");
        }

        var handler = request.Services.GetKeyedService<IMcpResourceTypeHandler>(entry.Source);

        if (handler is null)
        {
            throw new McpException($"No handler found for resource type '{entry.Source}'.");
        }

        // For concrete URIs (no template variables), pass empty variables.
        if (!McpResourceUri.IsTemplate(entry.Resource.Uri))
        {
            return await handler.ReadAsync(entry, new Dictionary<string, string>(), cancellationToken);
        }

        // For template URIs, extract variables from the pattern match.
        if (McpResourceUri.TryMatch(entry.Resource.Uri, uri, out var variables))
        {
            return await handler.ReadAsync(entry, variables, cancellationToken);
        }

        throw new McpException($"Resource URI '{uri}' does not match the expected pattern '{entry.Resource.Uri}'.");
    }
}
