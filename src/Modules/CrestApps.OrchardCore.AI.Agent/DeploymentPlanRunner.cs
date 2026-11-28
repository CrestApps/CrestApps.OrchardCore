using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Services;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Runs one deployment step through the deployment manager, the service the export targets use, and keeps the recipe
/// steps it builds in memory. Nothing is written to disk, and the files a step adds are dropped.
/// </summary>
internal static class DeploymentPlanRunner
{
    /// <summary>
    /// Runs the one deployment step of a registered step type, such as the step that exports every admin menu.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="stepTypeName">The name of the deployment step type, such as <c>AdminMenuDeploymentStep</c>.</param>
    /// <returns>The recipe steps the step added, or <see langword="null"/> when the step type is not registered.</returns>
    public static async Task<IList<JsonObject>> RunStepAsync(IServiceProvider services, string stepTypeName)
    {
        ArgumentNullException.ThrowIfNull(services);

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

        var result = new DeploymentPlanResult(new DiscardingFileBuilder(), new RecipeDescriptor());

        await services.GetRequiredService<IDeploymentManager>().ExecuteDeploymentPlanAsync(plan, result);

        return result.Steps;
    }

    private sealed class DiscardingFileBuilder : IFileBuilder
    {
        public Task SetFileAsync(string subpath, Stream stream)
            => Task.CompletedTask;
    }
}
