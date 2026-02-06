using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public sealed class ListRecipeStepsAndSchemasTool : AIFunction
{
    public const string TheName = "listOrchardCoreRecipeStepsAndSchemas";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Lists all available Orchard Core recipe steps and returns their JSON schema definitions.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var recipeSchemaService = arguments.Services.GetRequiredService<RecipeSchemaService>();
        var recipeSteps = arguments.Services.GetRequiredService<IEnumerable<IRecipeStep>>();

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var stepName in recipeSchemaService.GetStepNames())
        {
            JsonSchema schema = null;

            foreach (var recipeStep in recipeSteps)
            {
                if (!string.Equals(recipeStep.Name, stepName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                schema = await recipeStep.GetSchemaAsync();
                break;
            }

            if (schema is null)
            {
                schema = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("name", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum(stepName)))
                    .Required("name")
                    .Build();
            }

            result[stepName] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(schema));
        }

        return JsonSerializer.Serialize(result);
    }
}
