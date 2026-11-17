using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Runs one recipe step right away, as the recipe executor runs each step of a recipe: every registered step handler
/// gets the step, and the one that owns its name runs it. Unlike importing a recipe, the step runs inside the tool
/// call, so the tool sees the errors the step reports.
/// </summary>
internal static class RecipeStepRunner
{
    /// <summary>
    /// Runs a recipe step.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="stepName">The name of the step, such as <c>AdminMenu</c>.</param>
    /// <param name="step">The step, with its <c>name</c> and its data.</param>
    /// <returns>The errors the step reported, empty when it succeeded.</returns>
    public static async Task<IList<string>> RunAsync(IServiceProvider services, string stepName, JsonObject step)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(stepName);
        ArgumentNullException.ThrowIfNull(step);

        var context = new RecipeExecutionContext
        {
            Name = stepName,
            Step = step,
        };

        foreach (var handler in services.GetServices<IRecipeStepHandler>())
        {
            await handler.ExecuteAsync(context);
        }

        return context.Errors;
    }
}
