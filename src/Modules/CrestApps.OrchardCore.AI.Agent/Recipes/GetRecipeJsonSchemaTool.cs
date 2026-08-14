using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

/// <summary>
/// Represents the get recipe json schema tool.
/// </summary>
public sealed class GetRecipeJsonSchemaTool : AIFunction
{
    public const string TheName = "getOrchardCoreRecipeJsonSchema";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "step": {
          "type": "string",
          "description": "Optional. A recipe step name whose definition should be included in the recipe schema. The returned schema always describes the root recipe object with a steps array, and the step name suggestions still include every known recipe step."
        }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Returns the Orchard Core recipe JSON Schema. The response always describes the root recipe object with a steps array; optionally limit it to one step definition while keeping all valid step names available. Call this immediately before importOrchardCoreRecipe whenever it is available, then build the recipe JSON to match that schema.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<GetRecipeJsonSchemaTool>>();
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var hasStep = arguments.TryGetFirstString("step", out var requestedStep) && !string.IsNullOrWhiteSpace(requestedStep);
        var recipeSchemaService = arguments.Services.GetRequiredService<RecipeSchemaService>();
        var rootSchema = await recipeSchemaService.GetRecipeSchemaAsync(hasStep ? requestedStep : null, cancellationToken);

        if (rootSchema is null)
        {
            var availableSteps = string.Join(", ", recipeSchemaService.GetStepNames());
            logger.LogWarning("AI tool '{ToolName}': unknown recipe step '{StepName}'.", Name, requestedStep);

            return $"Unknown recipe step '{requestedStep}'. Available steps: {availableSteps}";
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return JsonSerializer.Serialize(rootSchema);
    }
}
