using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

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

        var recipeStepsService = arguments.Services.GetRequiredService<RecipeStepsService>();
        var recipeSteps = arguments.Services.GetRequiredService<IEnumerable<IRecipeStep>>();

        arguments.TryGetFirstString("step", out var requestedStep);

        var stepSchemas = new Dictionary<string, JsonSchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var stepName in recipeStepsService.GetRecipeStepNames())
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
            return stepSchemas.TryGetValue(requestedStep, out var schema)
                ? JsonSerializer.Serialize(schema)
                : $"Unknown recipe step '{requestedStep}'.";
        }

        var stepsBuilder = new JsonSchemaBuilder().OneOf(stepSchemas.Values);

        var rootSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("steps", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(stepsBuilder)
                    .MinItems(1)))
            .Required("steps")
            .Build();

        return JsonSerializer.Serialize(rootSchema);
    }
}
