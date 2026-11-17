using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Services;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Runs a deployment plan through the deployment manager, the service the export targets use, and keeps the result
/// in memory: the recipe the plan builds and the names and sizes of the other files it adds. Nothing is written to
/// disk.
/// </summary>
internal static class DeploymentPlanRunner
{
    /// <summary>
    /// The name of the recipe file a deployment plan builds.
    /// </summary>
    public const string RecipeFileName = "Recipe.json";

    /// <summary>
    /// Runs a deployment plan.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="plan">The plan to run.</param>
    /// <returns>The recipe the plan built and the other files it added.</returns>
    public static async Task<DeploymentRunResult> RunAsync(IServiceProvider services, DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(plan);

        var fileBuilder = new InMemoryFileBuilder();
        var result = new DeploymentPlanResult(fileBuilder, new RecipeDescriptor());

        await services.GetRequiredService<IDeploymentManager>().ExecuteDeploymentPlanAsync(plan, result);

        return new DeploymentRunResult(result.Recipe, result.Steps.ToList(), fileBuilder.RecipeLength, fileBuilder.OtherFiles);
    }

    /// <summary>
    /// Runs the one deployment step of a registered step type, such as the step that exports every admin menu.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="stepTypeName">The name of the deployment step type, such as <c>AdminMenuDeploymentStep</c>.</param>
    /// <returns>The recipe steps the step added, or <see langword="null"/> when the step type is not registered.</returns>
    public static async Task<IList<JsonObject>> RunStepAsync(IServiceProvider services, string stepTypeName)
    {
        var factory = services.GetServices<IDeploymentStepFactory>().FirstOrDefault(factory => factory.Name == stepTypeName);

        if (factory is null)
        {
            return null;
        }

        var plan = new DeploymentPlan
        {
            Name = stepTypeName,
        };

        plan.DeploymentSteps.Add(factory.Create());

        return (await RunAsync(services, plan)).Steps;
    }

    private sealed class InMemoryFileBuilder : IFileBuilder
    {
        public long RecipeLength { get; private set; }

        public List<(string Path, long Length)> OtherFiles { get; } = [];

        public async Task SetFileAsync(string subpath, Stream stream)
        {
            long length;

            if (stream.CanSeek)
            {
                length = stream.Length - stream.Position;
            }
            else
            {
                // Only the size is kept: the content of a file other than the recipe never leaves the plan.
                var buffer = new byte[81920];
                length = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    length += read;
                }
            }

            if (string.Equals(subpath, RecipeFileName, StringComparison.OrdinalIgnoreCase))
            {
                RecipeLength = length;

                return;
            }

            OtherFiles.Add((subpath, length));
        }
    }
}

/// <summary>
/// The result of a deployment plan run in memory.
/// </summary>
/// <param name="Recipe">The recipe the plan built, with its steps.</param>
/// <param name="Steps">The recipe steps.</param>
/// <param name="RecipeLength">The size of the recipe file, in bytes.</param>
/// <param name="OtherFiles">The other files the plan added, such as media files, with their size in bytes.</param>
internal sealed record DeploymentRunResult(JsonObject Recipe, IList<JsonObject> Steps, long RecipeLength, List<(string Path, long Length)> OtherFiles)
{
    /// <summary>
    /// Gets the recipe as the indented JSON text of the file.
    /// </summary>
    public string RecipeText
        => Recipe.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>
    /// Gets the size of the recipe text in UTF-8 bytes.
    /// </summary>
    public int RecipeTextLength
        => Encoding.UTF8.GetByteCount(RecipeText);
}
