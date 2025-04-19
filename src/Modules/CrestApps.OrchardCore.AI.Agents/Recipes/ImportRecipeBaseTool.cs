using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Agents.Recipes;

public abstract class ImportRecipeBaseTool : AIFunction
{
    private readonly IEnumerable<IDeploymentTargetHandler> _deploymentTargetHandlers;

    public override JsonElement JsonSchema { get; }

    protected ImportRecipeBaseTool(IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers)
    {

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "type": "object",
                "properties": {
                    "recipe": {
                        "type": "string",
                        "description": "A JSON object representing an OrchardCore recipe"
                    }
                },
                "additionalProperties": false,
                "required": ["recipe"]
            }
            """, JsonSerializerOptions);
        _deploymentTargetHandlers = deploymentTargetHandlers;
    }

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("recipe", out var recipe))
        {
            return ValueTask.FromResult<object>(MissingArgument());
        }

        return ProcessRecipeAsync(recipe, cancellationToken);
    }

    protected static string MissingArgument(string name = "recipe")
    {
        return $"Unable to find a '{name}' argument in the arguments parameter.";
    }

    protected async ValueTask<object> ProcessRecipeAsync(object recipe, CancellationToken cancellationToken)
    {
        var tempArchiveName = PathExtensions.GetTempFileName() + ".json";
        var tempArchiveFolder = PathExtensions.GetTempFileName();

        var json = recipe switch
        {
            JsonElement jsonElement => jsonElement.ToString(),
            _ => recipe.ToString(),
        };

        try
        {
            using (var stream = new FileStream(tempArchiveName, FileMode.Create))
            {
                var bytes = Encoding.UTF8.GetBytes(json);

                await stream.WriteAsync(bytes, cancellationToken);
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
