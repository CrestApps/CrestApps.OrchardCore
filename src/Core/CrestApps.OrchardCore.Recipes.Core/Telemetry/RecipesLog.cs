using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Recipes.Core.Telemetry;

/// <summary>
/// The log messages of recipe executions. Their event ids start at <see cref="CrestAppsEventIds.ContentTransfer"/> + 700.
/// </summary>
internal static partial class RecipesLog
{
    /// <summary>
    /// Logs that a recipe starts executing.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="stepCount">The number of steps of the recipe.</param>
    /// <param name="handlerCount">The number of deployment target handlers the recipe goes through.</param>
    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 700,
        Level = LogLevel.Information,
        Message = "Executing a recipe of {StepCount} steps through {HandlerCount} deployment target handlers.")]
    public static partial void ExecutingRecipe(ILogger logger, int stepCount, int handlerCount);

    /// <summary>
    /// Logs that a recipe executed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="stepCount">The number of steps of the recipe.</param>
    /// <param name="elapsedMilliseconds">How long the execution took, in milliseconds.</param>
    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 701,
        Level = LogLevel.Information,
        Message = "Executed a recipe of {StepCount} steps in {ElapsedMilliseconds} ms.")]
    public static partial void ExecutedRecipe(ILogger logger, int stepCount, double elapsedMilliseconds);
}
