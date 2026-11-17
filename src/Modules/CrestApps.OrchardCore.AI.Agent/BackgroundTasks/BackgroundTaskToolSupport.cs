using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.BackgroundTasks;

/// <summary>
/// What the background task tools share: the tasks of the tenant with their settings, read as the Background Tasks
/// page reads them, and the monitor of the background service, which may not be registered.
/// </summary>
internal static class BackgroundTaskToolSupport
{
    /// <summary>
    /// The note every result carries about where the execution state comes from.
    /// </summary>
    public const string StateNote = "The execution state is kept in memory by the server that answered, since it started; on a web farm another server can show other runs.";

    /// <summary>
    /// Gets the monitor of the background service, as the Background Tasks page does: it is registered with the
    /// background service, so it can be missing.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The monitor, or <see langword="null"/> when the background service is not registered.</returns>
    public static IBackgroundTaskMonitor GetMonitor(IServiceProvider services)
        => services.GetServices<IBackgroundTaskMonitor>().LastOrDefault();

    /// <summary>
    /// Gets the settings a task runs with: the saved settings, or the defaults the task declares when none are saved.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="task">The task.</param>
    /// <returns>The settings.</returns>
    public static async Task<BackgroundTaskSettings> GetSettingsAsync(IServiceProvider services, IBackgroundTask task)
    {
        var settingsProvider = services.GetService<IBackgroundTaskSettingsProvider>();

        var settings = settingsProvider is not null
            ? await settingsProvider.GetSettingsAsync(task)
            : null;

        return settings ?? task.GetDefaultSettings();
    }

    /// <summary>
    /// Describes a completed run for a tool result, in the user's or the site's time zone. The stack trace of a
    /// failure is left out; the error message is enough to explain it.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <param name="run">The run.</param>
    /// <returns>The run, or <see langword="null"/> when there is none.</returns>
    public static async Task<object> DescribeRunAsync(ILocalClock localClock, BackgroundTaskRun run)
    {
        if (run is null)
        {
            return null;
        }

        return new
        {
            startTime = await AgentToolResults.ToLocalAsync(localClock, run.StartTime),
            endTime = await AgentToolResults.ToLocalAsync(localClock, run.EndTime),
            durationSeconds = Math.Round(run.Duration.TotalSeconds, 3),
            trigger = run.Trigger.ToString(),
            result = run.Result.ToString(),
            errorMessage = string.IsNullOrEmpty(run.ErrorMessage) ? null : run.ErrorMessage,
        };
    }

    /// <summary>
    /// Converts a start time of the state, where <see cref="DateTime.MinValue"/> means "never".
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time, or <see langword="null"/> when there is none.</returns>
    public static Task<string> ToLocalAsync(ILocalClock localClock, DateTime? utc)
        => utc is null || utc.Value == DateTime.MinValue
            ? Task.FromResult<string>(null)
            : AgentToolResults.ToLocalAsync(localClock, utc);
}
