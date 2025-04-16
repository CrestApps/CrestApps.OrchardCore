using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.FileProviders;
using OrchardCore.Deployment.Services;

namespace CrestApps.OrchardCore.AI.Tools.Recipes;

public abstract class ImportRecipeBaseTool : AIFunction
{
    protected readonly IDeploymentManager DeploymentManager;

    public override JsonElement JsonSchema { get; }

    protected ImportRecipeBaseTool(IDeploymentManager deploymentManager)
    {
        DeploymentManager = deploymentManager;

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

            await DeploymentManager.ImportDeploymentPackageAsync(new PhysicalFileProvider(tempArchiveFolder));

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

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("recipe", out var recipe))
        {
            return ValueTask.FromResult<object>("Unable to find a recipe argument in the function arguments.");
        }

        return ProcessRecipeAsync(recipe, cancellationToken);
    }
}
