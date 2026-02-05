using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles recipe-schema:// URI resources by providing JSON schema definitions for recipe steps or content types.
/// Supports patterns:
/// - recipe-schema://recipe-step/{stepName} - Get the JSON schema for a specific recipe step
/// - recipe-schema://content-type/{contentTypeName} - Get the JSON schema for a specific content type
/// </summary>
public sealed class RecipeSchemaResourceTypeHandler : IMcpResourceTypeHandler
{
    public const string TypeName = "recipe-schema";

    private readonly IEnumerable<IRecipeStep> _recipeSteps;
    private readonly ILogger _logger;

    public RecipeSchemaResourceTypeHandler(
        IEnumerable<IRecipeStep> recipeSteps,
        ILogger<RecipeSchemaResourceTypeHandler> logger)
    {
        _recipeSteps = recipeSteps;
        _logger = logger;
    }

    public string Type => TypeName;

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, CancellationToken cancellationToken = default)
    {
        var uri = resource.Resource?.Uri;

        if (string.IsNullOrEmpty(uri))
        {
            throw new InvalidOperationException("Resource URI is required.");
        }

        // Parse the recipe-schema:// URI
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var schemaUri) || schemaUri.Scheme != "recipe-schema")
        {
            throw new InvalidOperationException($"Invalid recipe-schema URI: {uri}. Expected format: recipe-schema://recipe-step/{{stepName}} or recipe-schema://content-type/{{contentTypeName}}");
        }

        // Parse the path segments: host is "recipe-step" or "content-type", path contains the name
        var host = schemaUri.Host.ToLowerInvariant();
        var path = schemaUri.AbsolutePath.TrimStart('/');

        _logger.LogDebug("Reading recipe-schema resource: host={Host}, path={Path}", host, path);

        string schema;

        if (host == "recipe-step")
        {
            // Get the JSON schema for a specific recipe step
            schema = await GetRecipeStepSchemaAsync(path, cancellationToken);
        }
        else if (host == "content-type")
        {
            // Get the JSON schema for a content type
            // For content types, we use the ContentDefinition recipe step
            schema = await GetContentTypeSchemaAsync(path, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException($"Invalid recipe-schema URI host: {host}. Expected 'recipe-step' or 'content-type'.");
        }

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri,
                    MimeType = "application/schema+json",
                    Text = schema,
                }
            ]
        };
    }

    private async Task<string> GetRecipeStepSchemaAsync(string stepName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(stepName))
        {
            throw new InvalidOperationException("Recipe step name is required.");
        }

        var recipeStep = _recipeSteps.FirstOrDefault(s => string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));

        if (recipeStep is null)
        {
            throw new InvalidOperationException($"Recipe step not found: {stepName}. Available steps: {string.Join(", ", _recipeSteps.Select(s => s.Name))}");
        }

        var schema = await recipeStep.GetSchemaAsync();

        return schema.ToString();
    }

    private async Task<string> GetContentTypeSchemaAsync(string contentTypeName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contentTypeName))
        {
            throw new InvalidOperationException("Content type name is required.");
        }

        // For content types, we delegate to the ContentDefinition recipe step
        // which provides the schema for content type definitions
        var contentDefinitionStep = _recipeSteps.FirstOrDefault(s => string.Equals(s.Name, "ContentDefinition", StringComparison.OrdinalIgnoreCase));

        if (contentDefinitionStep is null)
        {
            throw new InvalidOperationException("ContentDefinition recipe step not found. Make sure the CrestApps.OrchardCore.AI.Agent module is enabled.");
        }

        var schema = await contentDefinitionStep.GetSchemaAsync();

        return schema.ToString();
    }
}
