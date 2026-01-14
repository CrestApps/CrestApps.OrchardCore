using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Core;

public abstract class ImportRecipeBaseTool : AIFunction
{
    protected readonly static JsonSerializerOptions RecipeSerializerOptions = new(JOptions.Default)
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string _jsonSchemaString =
        """
        {
          "type": "object",
          "properties": {
            "recipe": {
              "type": "string",
              "description": "A JSON string representing an Orchard Core recipe to import."
            }
          },
          "required": ["recipe"],
          "additionalProperties": false
        }
        """;

    public override JsonElement JsonSchema => JsonSerializer.Deserialize<JsonElement>(_jsonSchemaString);

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return ValueTask.FromResult<object>(MissingArgument());
        }

        return ProcessRecipeAsync(arguments.Services, recipe, cancellationToken);
    }

    protected static string MissingArgument(string name = "recipe")
    {
        return $"Unable to find a '{name}' argument in the arguments parameter.";
    }

#pragma warning disable IDE0060 // Remove unused parameter
    protected static async ValueTask<object> ProcessRecipeAsync(IServiceProvider services, string json, CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        var recipeExecutionService = services.GetRequiredService<RecipeExecutionService>();
        var recipeStepsService = services.GetRequiredService<RecipeStepsService>();
        var recipeSteps = services.GetRequiredService<IEnumerable<IRecipeStep>>();

        var data = JsonSerializer.Deserialize<JsonObject>(json, RecipeSerializerOptions);

        var stepSchemas = new Dictionary<string, JsonSchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var stepName in recipeStepsService.GetRecipeStepNames())
        {
            if (stepSchemas.ContainsKey(stepName))
            {
                continue;
            }

            var added = false;

            foreach (var recipeStep in recipeSteps)
            {
                if (!string.Equals(recipeStep.Name, stepName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var stepSchema = await recipeStep.GetSchemaAsync();

                if (stepSchema is not null)
                {
                    stepSchemas[stepName] = stepSchema;
                    added = true;
                    break;
                }
            }

            if (added)
            {
                continue;
            }

            var simpleStepBuilder = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("name", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Enum(stepName))
                        )
                        .Required("name");

            stepSchemas[stepName] = simpleStepBuilder.Build();
        }

        var stepsBuilder = new JsonSchemaBuilder().OneOf(stepSchemas.Values);

        var schemaBuilder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("steps", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(stepsBuilder)
                    .MinItems(1)
                )
            ).Required("steps");

        var rootSchema = schemaBuilder.Build();

        var result = rootSchema.Evaluate(data, new EvaluationOptions()
        {
            OutputFormat = OutputFormat.List,
        });

        if (!result.IsValid)
        {
            var schemaStructure = JsonSerializer.Serialize(rootSchema);

            return
            $"""
                Invalid recipe format. The recipe must match the expected schema shown below. 
                Please generate a valid recipe and try again:
                {schemaStructure}
            """;
        }

        if (await recipeExecutionService.ExecuteRecipeAsync(data))
        {
            return "Recipe was successfully imported";
        }

        return "Error occurred while trying to import the recipe.";
    }
}
