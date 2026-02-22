using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles recipe-step-schema resources by providing the JSON schema for a specific recipe step.
/// Supported variable: {stepName} - the name of the recipe step.
/// </summary>
public sealed class RecipeStepSchemaResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "recipe-step-schema";

    private readonly RecipeSchemaService _recipeSchemaService;
    private readonly ILogger _logger;

    public RecipeStepSchemaResourceTypeHandler(
        RecipeSchemaService recipeSchemaService,
        ILogger<RecipeStepSchemaResourceTypeHandler> logger)
        : base(TypeName)
    {
        _recipeSchemaService = recipeSchemaService;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        variables.TryGetValue("stepName", out var stepName);

        if (string.IsNullOrEmpty(stepName))
        {
            return CreateErrorResult(resource.Resource.Uri, "Step name is required. Include {stepName} in the URI pattern.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading recipe step schema for step: {StepName}", stepName);
        }

        var stepSchema = await _recipeSchemaService.GetStepSchemaAsync(stepName);

        if (stepSchema is null)
        {
            return CreateErrorResult(resource.Resource.Uri, $"Recipe step not found: '{stepName}'. Available steps: {string.Join(", ", _recipeSchemaService.GetStepNames())}");
        }

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = resource.Resource.Uri,
                    MimeType = "application/schema+json",
                    Text = JsonSerializer.Serialize(stepSchema),
                }
            ]
        };
    }
}
