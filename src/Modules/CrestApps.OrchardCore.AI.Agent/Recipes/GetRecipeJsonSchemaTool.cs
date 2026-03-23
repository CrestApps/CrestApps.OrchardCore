using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

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
              "description": "Optional. A recipe step name to return only that step schema. If omitted, returns the full recipe schema (steps array) composed from all known step schemas."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Returns a JSON Schema definition for Orchard Core recipes or a specific recipe step.";

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

        var recipeSchemaService = arguments.Services.GetRequiredService<RecipeSchemaService>();
        var recipeSteps = arguments.Services.GetRequiredService<IEnumerable<IRecipeStep>>();

        arguments.TryGetFirstString("step", out var requestedStep);

        var stepSchemas = new Dictionary<string, JsonSchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var stepName in recipeSchemaService.GetStepNames())
        {
            if (!string.IsNullOrWhiteSpace(requestedStep)
                && !string.Equals(requestedStep, stepName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (stepSchemas.ContainsKey(stepName))
            {
                continue;
            }

            JsonSchema stepSchema = null;

            foreach (var recipeStep in recipeSteps)
            {
                if (!string.Equals(recipeStep.Name, stepName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                stepSchema = await recipeStep.GetSchemaAsync();
                break;
            }

            if (stepSchema is null)
            {
                stepSchema = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("name", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum(stepName)))
                    .Required("name")
                    .Build();
            }

            stepSchemas[stepName] = stepSchema;

            if (!string.IsNullOrWhiteSpace(requestedStep))
            {
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(requestedStep))
        {
            if (!stepSchemas.TryGetValue(requestedStep, out var schema))
            {
                logger.LogWarning("AI tool '{ToolName}': unknown recipe step '{StepName}'.", Name, requestedStep);

                return $"Unknown recipe step '{requestedStep}'.";
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }

            return JsonSerializer.Serialize(schema);
        }

        var stepsBuilder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(stepSchemas.Keys)))
            .Required("name")
            .AdditionalProperties(true);

        var rootSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("steps", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(stepsBuilder)
                    .MinItems(1)))
            .Required("steps")
            .Build();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return JsonSerializer.Serialize(rootSchema);
    }
}
