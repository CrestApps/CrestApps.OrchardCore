using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.BackgroundTasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list the background tasks and run them on demand, when the Background Tasks feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.BackgroundTasks)]
public sealed class BackgroundTasksToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundTasksToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BackgroundTasksToolsStartup(IStringLocalizer<BackgroundTasksToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListBackgroundTasksTool>(ListBackgroundTasksTool.TheName)
            .WithTitle(S["List Background Tasks"])
            .WithDescription(S["Lists the site's background tasks with schedule, enabled state, current status, next start, and run and failure counts; pass a task name to also get its recent runs and errors. Use it to answer why a task did not run or failed."])
            .WithCategory(S["Background Tasks"])
            .Selectable();

        services.AddCoreAITool<RunBackgroundTaskTool>(RunBackgroundTaskTool.TheName)
            .WithTitle(S["Run Background Task"])
            .WithDescription(S["Queues one or more background tasks to run now, outside their schedule. It returns once the run is requested; use listBackgroundTasks afterwards to see the result."])
            .WithCategory(S["Background Tasks"])
            .Selectable();
    }
}
