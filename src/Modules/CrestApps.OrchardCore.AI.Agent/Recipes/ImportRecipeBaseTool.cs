using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using OrchardCore.Deployment;
using OrchardCore.Json;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public abstract class ImportRecipeBaseTool : AIFunction
{
    private readonly IEnumerable<IDeploymentTargetHandler> _deploymentTargetHandlers;
    private readonly IEnumerable<IRecipeStepHandler> _handlers;
    private readonly DocumentJsonSerializerOptions _options;

    public override JsonElement JsonSchema { get; }

    protected ImportRecipeBaseTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        IEnumerable<IRecipeStepHandler> handlers,
        DocumentJsonSerializerOptions options)
    {
        _deploymentTargetHandlers = deploymentTargetHandlers;
        _handlers = handlers;
        _options = options;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
              "type": "object",
              "properties": {
                "recipe": {
                  "type": "string",
                  "description": "A JSON string representing an Orchard Core recipe."
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


        var stepsBuilder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object);

        var stepNameDefined = await BuildingRecipeSchemaAsync(stepsBuilder);

        if (!stepNameDefined)
        {
            var stepNames = _handlers
                .Where(h =>
                    h.GetType() == typeof(NamedRecipeStepHandler) ||
                    h.GetType().IsSubclassOf(typeof(NamedRecipeStepHandler)))
                .Select(h =>
                    (string)h.GetType()
                        .GetField("StepName", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.GetValue(h))
                .Where(name => name != null)
                .Distinct()
                .ToArray();

            if (stepNames.Length > 0)
            {
                stepsBuilder.Properties(
                    ("name", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Enum(stepNames)
                    )
                ).Required("name");
            }
        }

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
                Invalid recipe given. The excepted recipe schema is:
                {JsonSerializer.Serialize(rootSchema, JsonHelpers.Indented)}
            """;
        }

        // var data = UpdateJsonData(rawData);

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

    abstract protected ValueTask<bool> BuildingRecipeSchemaAsync(JsonSchemaBuilder builder);
}
