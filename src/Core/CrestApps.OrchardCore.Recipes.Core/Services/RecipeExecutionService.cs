using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Recipes.Core.Telemetry;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.FileStorage;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Provides functionality to execute recipe data by writing it to a temporary file
/// and dispatching it through the deployment target handlers.
/// </summary>
public sealed class RecipeExecutionService
{
    private readonly IEnumerable<IDeploymentTargetHandler> _deploymentTargetHandlers;
    private readonly DocumentJsonSerializerOptions _options;
    private readonly ITempDirectoryProvider _tempDirectoryProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipeExecutionService"/> class.
    /// </summary>
    /// <param name="deploymentTargetHandlers">The deployment target handlers that process recipe files.</param>
    /// <param name="options">The document JSON serializer options.</param>
    /// <param name="tempDirectoryProvider">The provider used to resolve tenant-scoped temporary file locations.</param>
    /// <param name="logger">The logger instance.</param>
    public RecipeExecutionService(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        IOptions<DocumentJsonSerializerOptions> options,
        ITempDirectoryProvider tempDirectoryProvider,
        ILogger<RecipeExecutionService> logger)
    {
        _deploymentTargetHandlers = deploymentTargetHandlers;
        _options = options.Value;
        _tempDirectoryProvider = tempDirectoryProvider;
        _logger = logger;
    }

    /// <summary>
    /// Executes a recipe from the provided JSON data by writing it to a temporary file
    /// and importing it through all registered deployment target handlers.
    /// </summary>
    /// <param name="data">The JSON node containing the recipe data to execute.</param>
    public async Task<bool> ExecuteRecipeAsync(JsonNode data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var handlers = _deploymentTargetHandlers.ToArray();
        var stepCount = data is JsonObject recipe && recipe["steps"] is JsonArray steps ? steps.Count : 0;

        using var telemetry = TelemetryScope.Start(RecipesTelemetry.ActivitySource, RecipesTelemetry.ExecuteSpanName, RecipesTelemetry.ExecutionDuration);
        telemetry.SetSpanTag(RecipesTelemetry.StepCountTag, stepCount);

        RecipesLog.ExecutingRecipe(_logger, stepCount, handlers.Length);

        var tempArchiveName = _tempDirectoryProvider.GetTempFileName(".json");
        var tempArchiveFolder = _tempDirectoryProvider.GetTempFileName();

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

            foreach (var deploymentTargetHandler in handlers)
            {
                // Each handler gets a child span, so a slow or failing handler shows in the trace of the execution.
                using var handlerTelemetry = TelemetryScope.Start(RecipesTelemetry.ActivitySource, RecipesTelemetry.DeploymentTargetSpanName, duration: null);
                handlerTelemetry.SetSpanTag(RecipesTelemetry.HandlerTag, deploymentTargetHandler.GetType().Name);

                try
                {
                    // Don't trigger in parallel to avoid potential race conditions in the handlers
                    await deploymentTargetHandler.ImportFromFileAsync(deploymentPackage);
                    handlerTelemetry.Succeed();
                }
                catch (Exception ex)
                {
                    handlerTelemetry.Fail(ex);

                    throw;
                }
            }

            telemetry.Succeed();
            RecipesLog.ExecutedRecipe(_logger, stepCount, telemetry.Elapsed.TotalMilliseconds);

            return true;
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);
            _logger.LogError(ex, "An error occurred while executing a recipe.");

            return false;
        }
        finally
        {
            RecipesTelemetry.RecordExecution(telemetry.Outcome);

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
