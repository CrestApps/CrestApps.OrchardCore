using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using OrchardCore.Deployment;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agents.Recipes;

public abstract class ImportRecipeBaseTool : AIFunction
{
    private readonly IEnumerable<IDeploymentTargetHandler> _deploymentTargetHandlers;
    private readonly DocumentJsonSerializerOptions _options;

    public override JsonElement JsonSchema { get; }

    protected ImportRecipeBaseTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        DocumentJsonSerializerOptions options)
    {
        _deploymentTargetHandlers = deploymentTargetHandlers;
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

        if (!data.ContainsKey("steps"))
        {
            data = new JsonObject
            {
                ["steps"] = new JsonArray(data)
            };
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
