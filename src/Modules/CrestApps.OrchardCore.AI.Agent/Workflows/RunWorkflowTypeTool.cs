using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking.Distributed;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Helpers;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Starts an instance of a workflow with values for its input variables, as the Run dialog of the designer does: only
/// an enabled workflow whose published version starts with Started by Workflow, under the lock of its type when it
/// runs one instance at a time, with each input coerced to its variable's type. For a workflow that starts with an HTTP
/// Request event it returns how to call it instead, as the run would read the chat's own request.
/// </summary>
public sealed class RunWorkflowTypeTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "runWorkflowType";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "workflowTypeId": {
          "type": "string",
          "description": "The workflow type ID, numeric ID or exact name of the workflow to run, from listWorkflowTypes."
        },
        "inputs": {
          "type": "object",
          "description": "The values of the workflow's input variables, by variable name. Inputs that are left out keep their default value."
        }
      },
      "required": [
        "workflowTypeId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Starts a new instance of an enabled workflow's published version that starts with \"Started by Workflow\", with values for its input variables, and returns the instance id, status, fault and output variables. Workflows that start with another event cannot be started this way; for an HTTP Request start it returns the URL and method instead.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ExecuteWorkflows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadJsonObject(arguments, "inputs", out var inputs))
        {
            return "Nothing was run: inputs must be an object of values by variable name.";
        }

        var reference = arguments.GetFirstValueOrDefault<string>("workflowTypeId");
        var workflowType = await WorkflowToolSupport.FindWorkflowTypeAsync(services.GetRequiredService<IWorkflowTypeStore>(), reference);

        if (workflowType is null)
        {
            return $"Nothing was run: there is no workflow '{reference}'. Call listWorkflowTypes to find its ID.";
        }

        if (!workflowType.IsEnabled)
        {
            return $"Nothing was run: the workflow '{workflowType.Name}' is disabled. Enable it with setWorkflowTypeEnabled to run it.";
        }

        var starts = workflowType.Activities.Where(activity => activity.IsStart).ToList();
        var start = starts.FirstOrDefault(activity => activity.Name == WorkflowToolSupport.StartedByWorkflowEventName);

        if (start is null)
        {
            var httpRequest = starts.FirstOrDefault(activity => activity.Name == WorkflowToolSupport.HttpRequestEventName);

            if (httpRequest is null)
            {
                return $"Nothing was run: the workflow '{workflowType.Name}' starts with {string.Join(", ", starts.Select(activity => activity.Name).DefaultIfEmpty("no start activity"))}, so only that event starts it. Only a workflow that starts with Started by Workflow can be run by hand.";
            }

            return AgentToolResults.Json(new
            {
                started = false,
                startsWith = WorkflowToolSupport.HttpRequestEventName,
                url = httpRequest.Properties?["Url"] is JsonValue url && url.TryGetValue<string>(out var path) && !string.IsNullOrEmpty(path) ? path : null,
                method = httpRequest.Properties?["HttpMethod"] is JsonValue method && method.TryGetValue<string>(out var verb) && !string.IsNullOrEmpty(verb) ? verb : "GET",
                note = "This workflow starts when its URL receives a request, which a tool cannot send. Call the URL with this method; when the URL is empty, generate it in the designer first. Then use listWorkflowInstances to see the instance it started.",
            });
        }

        // As the other ways to start an instance do, a singleton is checked and started under the lock of its type.
        var (locker, locked) = await services.GetRequiredService<IDistributedLock>().TryAcquireWorkflowTypeLockAsync(workflowType);

        if (!locked)
        {
            return $"Nothing was run: the workflow '{workflowType.Name}' runs one instance at a time, and one is running.";
        }

        await using var acquiredLock = locker;

        var workflowStore = services.GetRequiredService<IWorkflowStore>();

        if (workflowType.IsSingleton && await workflowStore.HasHaltedInstanceAsync(workflowType.WorkflowTypeId))
        {
            return $"Nothing was run: the workflow '{workflowType.Name}' runs one instance at a time, and one is waiting.";
        }

        var variableTypes = services.GetService<IWorkflowVariableTypeProvider>();
        var inputVariables = workflowType.Variables.Where(variable => variable.IsInput).ToList();
        var input = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var invalid = new List<string>();

        foreach (var (name, value) in inputs ?? [])
        {
            var variable = inputVariables.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

            if (variable is null)
            {
                return $"Nothing was run: '{name}' is not an input of the workflow '{workflowType.Name}'. Its inputs are: {string.Join(", ", inputVariables.Select(item => item.Name).DefaultIfEmpty("none"))}.";
            }

            if (value is null)
            {
                continue;
            }

            if (variableTypes?.Get(variable.TypeName) is { } type && !type.TryCoerce(value, out _))
            {
                invalid.Add($"{variable.Name} ({variable.TypeName})");

                continue;
            }

            input[variable.Name] = value;
        }

        if (invalid.Count > 0)
        {
            return $"Nothing was run: these inputs do not have a value of their type: {string.Join(", ", invalid)}.";
        }

        var workflowContext = await services.GetRequiredService<IWorkflowManager>().StartWorkflowAsync(workflowType, start, input);
        var workflow = workflowContext.Workflow;
        var exists = workflow.Id != 0 && await workflowStore.GetAsync(workflow.Id) is not null;

        return AgentToolResults.Json(new
        {
            started = true,
            instanceId = exists ? workflow.Id : (long?)null,
            workflowId = workflow.WorkflowId,
            status = workflowContext.Status.ToString(),
            faultMessage = WorkflowToolSupport.Truncate(workflow.FaultMessage),
            outputs = workflowContext.Variables.GetOutputs().ToDictionary(
                output => output.Key,
                output => WorkflowToolSupport.FormatValue(output.Value),
                StringComparer.OrdinalIgnoreCase),
            instanceUrl = exists ? WorkflowToolSupport.GetInstancePath(services, workflow) : null,
            note = exists ? null : "The instance was not kept (the workflow deletes finished instances), so only this result shows what it did.",
        });
    }
}
