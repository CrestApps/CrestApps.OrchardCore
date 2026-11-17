using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.BackgroundTasks;

/// <summary>
/// Lists the background tasks of the tenant as the Background Tasks page and its Details page do: each registered
/// task with the settings it runs with and the execution state the background service keeps for it.
/// </summary>
public sealed class ListBackgroundTasksTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listBackgroundTasks";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The technical name of one task, from an earlier list. When given, only that task is returned, with its lock settings and its recent runs and errors."
        },
        "search": {
          "type": "string",
          "description": "Text the task title or description must contain. Leave it out to list every task."
        },
        "status": {
          "type": "string",
          "enum": [
            "enabled",
            "disabled",
            "running",
            "failed"
          ],
          "description": "Keeps only the enabled tasks, the disabled tasks, the tasks running or queued, or the tasks whose last run failed. Leave it out to keep every task."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of tasks on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the site's background tasks with schedule, enabled state, current status, next start, and run and failure counts; pass a task name to also get its recent runs and errors. Use it to answer why a task did not run or failed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageBackgroundTasks))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("name", out var name);
        arguments.TryGetFirstString("search", out var search);
        arguments.TryGetFirstString("status", out var status);
        name = name?.Trim();
        search = search?.Trim();
        status = status?.Trim();

        if (!string.IsNullOrEmpty(status) && status is not ("enabled" or "disabled" or "running" or "failed"))
        {
            return $"The status '{status}' is not known. Use enabled, disabled, running or failed, or leave it out.";
        }

        var tasks = services.GetServices<IBackgroundTask>();
        var monitor = BackgroundTaskToolSupport.GetMonitor(services);
        var tenant = services.GetRequiredService<ShellSettings>().Name;
        var localClock = services.GetRequiredService<ILocalClock>();

        if (!string.IsNullOrEmpty(name))
        {
            var task = tasks.GetTaskByName(name);

            if (task is null)
            {
                return $"There is no background task named '{name}'. Call listBackgroundTasks without a name to see the technical names.";
            }

            var state = monitor is not null
                ? await monitor.GetStateAsync(tenant, name)
                : null;

            return AgentToolResults.Json(new
            {
                task = await DescribeAsync(services, localClock, task, await BackgroundTaskToolSupport.GetSettingsAsync(services, task), state, monitor is not null, details: true),
                timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
                note = GetNote(monitor),
            });
        }

        var states = monitor is not null
            ? (await monitor.GetStatesAsync(tenant)).ToDictionary(state => state.Name, StringComparer.Ordinal)
            : [];

        var entries = new List<(IBackgroundTask Task, BackgroundTaskSettings Defaults, BackgroundTaskSettings Settings, BackgroundTaskState State)>();

        foreach (var task in tasks)
        {
            var defaults = task.GetDefaultSettings();
            var settings = await BackgroundTaskToolSupport.GetSettingsAsync(services, task);
            states.TryGetValue(defaults.Name, out var state);

            entries.Add((task, defaults, settings, state));
        }

        var filtered = entries
            .Where(entry => string.IsNullOrEmpty(search)
                || entry.Defaults.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                || entry.Settings.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .Where(entry => status switch
            {
                "enabled" => entry.Settings.Enable,
                "disabled" => !entry.Settings.Enable,
                "running" => entry.State is not null && entry.State.Status != BackgroundTaskStatus.Idle,
                "failed" => entry.State?.LastRun?.Result == BackgroundTaskRunResult.Failed,
                _ => true,
            })
            .OrderBy(entry => entry.Defaults.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var items = new List<object>();

        foreach (var entry in filtered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await DescribeAsync(services, localClock, entry.Task, entry.Settings, entry.State, monitor is not null, details: false));
        }

        return AgentToolResults.Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize
                ? true
                : (bool?)null,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            note = GetNote(monitor),
        });
    }

    private static async Task<object> DescribeAsync(
        IServiceProvider services,
        ILocalClock localClock,
        IBackgroundTask task,
        BackgroundTaskSettings settings,
        BackgroundTaskState state,
        bool isMonitored,
        bool details)
    {
        var defaults = task.GetDefaultSettings();

        List<object> recentRuns = null;

        if (details && state is not null)
        {
            recentRuns = [];

            foreach (var run in state.RecentRuns)
            {
                recentRuns.Add(await BackgroundTaskToolSupport.DescribeRunAsync(localClock, run));
            }
        }

        return new
        {
            name = defaults.Name,
            title = defaults.Title,
            description = string.IsNullOrEmpty(settings.Description) ? null : settings.Description,
            enabled = settings.Enable,
            schedule = settings.Schedule,
            defaultSchedule = details ? defaults.Schedule : null,
            lockTimeoutMilliseconds = details ? settings.LockTimeout : (int?)null,
            lockExpirationMilliseconds = details ? settings.LockExpiration : (int?)null,
            usePipeline = details ? settings.UsePipeline : (bool?)null,
            status = isMonitored ? (state?.Status.ToString() ?? "NotLoaded") : null,
            queuedTime = state is null ? null : await BackgroundTaskToolSupport.ToLocalAsync(localClock, state.QueuedTime),
            lastStartTime = state is null ? null : await BackgroundTaskToolSupport.ToLocalAsync(localClock, state.LastStartTime),
            nextStartTime = state is null ? null : await BackgroundTaskToolSupport.ToLocalAsync(localClock, state.NextStartTime),
            runCount = state?.RunCount,
            failureCount = state?.FailureCount,
            lastRun = state is null ? null : await BackgroundTaskToolSupport.DescribeRunAsync(localClock, state.LastRun),
            recentRuns,
            link = details
                ? AgentToolLinks.GetAdminPath(services, "OrchardCore.BackgroundTasks", "BackgroundTask", "Details", new() { ["name"] = defaults.Name })
                : null,
        };
    }

    private static string GetNote(IBackgroundTaskMonitor monitor)
        => monitor is null
            ? "The background service is not registered on this site, so there is no execution state and tasks cannot be run on demand."
            : BackgroundTaskToolSupport.StateNote + " A status of NotLoaded means the background service has not picked the task up yet.";
}
