using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Schemas;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Json.Schema;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public abstract class ImportRecipeBaseTool : AIFunction
{
    private readonly RecipeExecutionService _recipeExecutionService;
    private readonly RecipeStepsService _recipeStepsService;
    private readonly IEnumerable<IRecipeStep> _recipeSteps;

    public override JsonElement JsonSchema { get; }

    protected ImportRecipeBaseTool(
        RecipeExecutionService recipeExecutionService,
        RecipeStepsService RecipeStepsService,
        IEnumerable<IRecipeStep> recipeSteps)
    {
        _recipeExecutionService = recipeExecutionService;
        _recipeStepsService = RecipeStepsService;
        _recipeSteps = recipeSteps;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
            """, JsonSerializerOptions);
    }

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return ValueTask.FromResult<object>(MissingArgument());
        }

        return ProcessRecipeAsync(recipe);
    }

    protected static string MissingArgument(string name = "recipe")
    {
        return $"Unable to find a '{name}' argument in the arguments parameter.";
    }

    protected async ValueTask<object> ProcessRecipeAsync(string json)
    {
        var data = JsonSerializer.Deserialize<JsonObject>(json, JsonHelpers.RecipeSerializerOptions);

        var stepSchemas = new Dictionary<string, JsonSchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var stepName in _recipeStepsService.GetRecipeStepNames())
        {
            if (stepSchemas.ContainsKey(stepName))
            {
                continue;
            }

            var added = false;

            foreach (var recipeStep in _recipeSteps)
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

        if (await _recipeExecutionService.ExecuteRecipeAsync(data))
        {
            return "Recipe was successfully imported";
        }

        return "Error occurred while trying to import the recipe.";
    }
}
