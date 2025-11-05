using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

public sealed class RecipeExecutionService
{
    private readonly IEnumerable<IDeploymentTargetHandler> _deploymentTargetHandlers;
    private readonly DocumentJsonSerializerOptions _options;

    public RecipeExecutionService(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        IOptions<DocumentJsonSerializerOptions> options)
    {
        _deploymentTargetHandlers = deploymentTargetHandlers;
        _options = options.Value;
    }

    public async Task<bool> ExecuteRecipeAsync(JsonNode data)
    {
        ArgumentNullException.ThrowIfNull(data);

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

            return true;
        }
        catch
        {
            return false;
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
