using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.BackgroundTasks;

/// <summary>
/// Asks the background service to run tasks now, as the Run now action and bulk action of the Background Tasks page
/// do. The run happens later, on the server that answered; the tool only reports that it was requested.
/// </summary>
public sealed class RunBackgroundTaskTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "runBackgroundTask";

    private const int MaxTasks = 20;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "names": {
          "type": "array",
          "minItems": 1,
          "maxItems": 20,
          "items": {
            "type": "string"
          },
          "description": "The technical names of the tasks to run, from listBackgroundTasks."
        }
      },
      "required": [
        "names"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Queues one or more background tasks to run now, outside their schedule. It returns once the run is requested; use listBackgroundTasks afterwards to see the result.";

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

        if (!AgentToolArguments.TryReadStrings(arguments, "names", out var names) || names is null || names.Count == 0)
        {
            return "Nothing was run: give the technical names of the tasks, from listBackgroundTasks.";
        }

        if (names.Count > MaxTasks)
        {
            return $"Nothing was run: give at most {MaxTasks} tasks at a time.";
        }

        var monitor = BackgroundTaskToolSupport.GetMonitor(services);

        if (monitor is null)
        {
            return "Nothing was run: background tasks can't be run on demand because the background service is not registered on this site.";
        }

        var tasks = services.GetServices<IBackgroundTask>();
        var unknown = names.Where(name => tasks.GetTaskByName(name) is null).ToList();

        if (unknown.Count > 0)
        {
            return $"Nothing was run: there is no background task named {string.Join(", ", unknown.Select(name => $"'{name}'"))}. Call listBackgroundTasks to see the technical names.";
        }

        var tenant = services.GetRequiredService<ShellSettings>().Name;
        var results = new List<object>();

        foreach (var name in names)
        {
            var task = tasks.GetTaskByName(name);
            var result = await monitor.RequestRunAsync(tenant, name);

            results.Add(new
            {
                name,
                title = task.GetDefaultSettings().Title,
                result = result.ToString(),
                message = result switch
                {
                    BackgroundTaskRunRequestResult.Queued => "The run was requested and starts shortly.",
                    BackgroundTaskRunRequestResult.AlreadyQueued => "A run was already requested; nothing more was queued.",
                    BackgroundTaskRunRequestResult.AlreadyRunning => "The task is already running; no other run was queued.",
                    _ => "The background service has not loaded this task yet, so it can't run. Try again in a moment.",
                },
            });
        }

        return AgentToolResults.Json(new
        {
            requested = results,
            note = "A queued run has not finished yet. Call listBackgroundTasks with the task name in a moment to see whether it succeeded. " + BackgroundTaskToolSupport.StateNote,
        });
    }
}
