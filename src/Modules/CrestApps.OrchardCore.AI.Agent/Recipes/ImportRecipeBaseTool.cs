using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Schemas;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using OrchardCore.Deployment;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public abstract class ImportRecipeBaseTool : AIFunction
{
    private readonly IEnumerable<IDeploymentTargetHandler> _deploymentTargetHandlers;
    private readonly RecipeStepsService _recipeStepsService;
    private readonly IEnumerable<IRecipeStep> _recipeSteps;
    private readonly DocumentJsonSerializerOptions _options;

    public override JsonElement JsonSchema { get; }

    protected ImportRecipeBaseTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        RecipeStepsService RecipeStepsService,
        IEnumerable<IRecipeStep> recipeSteps,
        DocumentJsonSerializerOptions options)
    {
        _deploymentTargetHandlers = deploymentTargetHandlers;
        _recipeStepsService = RecipeStepsService;
        _recipeSteps = recipeSteps;
        _options = options;
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

        return ProcessRecipeAsync(recipe, cancellationToken);
    }

    protected static string MissingArgument(string name = "recipe")
    {
        return $"Unable to find a '{name}' argument in the arguments parameter.";
    }

    protected async ValueTask<object> ProcessRecipeAsync(string json, CancellationToken cancellationToken)
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
            return
            $"""
                Invalid recipe format. The recipe must match the expected schema shown below. 
                Please generate a valid recipe and try again:
                {JsonSerializer.Serialize(rootSchema)}
            """;
        }

        var tempArchiveName = PathExtensions.GetTempFileName() + ".json";
        var tempArchiveFolder = PathExtensions.GetTempFileName();

        try
        {
            using (var stream = new FileStream(tempArchiveName, FileMode.Create))
            {
                using var writer = new Utf8JsonWriter(stream);
                data.WriteTo(writer, _options.SerializerOptions);
            }

            Directory.CreateDirectory(tempArchiveFolder);
            File.Move(tempArchiveName, Path.Combine(tempArchiveFolder, "Recipe.json"));

            var deploymentPackage = new PhysicalFileProvider(tempArchiveFolder);

            foreach (var deploymentTargetHandler in _deploymentTargetHandlers)
            {
                // Don't trigger in parallel to avoid potential race conditions in the handlers
                await deploymentTargetHandler.ImportFromFileAsync(deploymentPackage);
            }

            return "Recipe was successfully imported";
        }
        catch
        {
            return "Error occurred while trying to import the recipe.";
        }
        finally
        {
            if (File.Exists(tempArchiveName))
            {
                File.Delete(tempArchiveName);
            }

            if (Directory.Exists(tempArchiveFolder))
            {
                Directory.Delete(tempArchiveFolder, true);
            }
        }
    }
}
