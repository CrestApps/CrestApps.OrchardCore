using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Parameters;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Tools;

/// <summary>
/// The tool a <c>Workflow</c> tool instance gives the model. Calling it runs the instance's workflow in this request
/// and waits for it; the workflow's output (set with <c>Set Output</c>) is returned to the model as JSON.
/// </summary>
/// <remarks>
/// The arguments are the instance's declared parameters, filled by the model, fixed by the instance or taken from the
/// conversation (the caller's number, the contact, a variable). An instance without parameters gives the model one
/// free-text <c>request</c> argument. A workflow that stops to wait for something (a timer, a signal) keeps running in
/// the background, and the model is told the request is in hand.
/// </remarks>
internal sealed class WorkflowToolFunction : AIFunction
{
    private readonly string _name;
    private readonly string _description;
    private readonly WorkflowToolSettings _settings;
    private readonly IReadOnlyList<AIToolInstanceParameter> _parameters;
    private readonly JsonElement _jsonSchema;
    private readonly Dictionary<string, object> _additionalProperties;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowToolFunction"/> class.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="description">What the tool does, for the model.</param>
    /// <param name="settings">The instance's settings.</param>
    /// <param name="instance">The tool instance.</param>
    public WorkflowToolFunction(string name, string description, WorkflowToolSettings settings, AIToolInstance instance)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(settings);

        _name = name;
        _description = description;
        _settings = settings;
        _parameters = AIToolParameterBinder.GetParameters(instance);

        var exposeRequest = _parameters.Count == 0;

        _jsonSchema = AIToolParameterSchemaBuilder.Merge(BuildBaseSchema(exposeRequest), _parameters);
        _additionalProperties = new Dictionary<string, object>
        {
            ["Strict"] = AIToolParameterSchemaBuilder.IsStrictEligible(exposeRequest, _parameters),
        };
    }

    /// <inheritdoc/>
    public override string Name => _name;

    /// <inheritdoc/>
    public override string Description => _description;

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties => _additionalProperties;

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var services = arguments.Services;
        var logger = services?.GetService<ILoggerFactory>()?.CreateLogger<WorkflowToolFunction>();

        if (services is null || string.IsNullOrWhiteSpace(_settings.WorkflowTypeId))
        {
            return Error("This tool is not connected to a workflow.");
        }

        var resolution = AIToolParameterBinder.Resolve(_parameters, arguments, services);

        if (!resolution.Succeeded)
        {
            // Reported to the model, which can correct the call and retry.
            return Error(string.Join(" ", resolution.Errors));
        }

        var workflowType = await services.GetRequiredService<IWorkflowTypeStore>().GetAsync(_settings.WorkflowTypeId);

        if (workflowType is null || !workflowType.IsEnabled)
        {
            return Error("The workflow behind this tool is missing or disabled.");
        }

        var startActivity = workflowType.Activities.FirstOrDefault(activity =>
            activity.IsStart && string.Equals(activity.Name, nameof(AIToolCalledEvent), StringComparison.Ordinal));

        if (startActivity is null)
        {
            return Error("The workflow behind this tool does not start with the AI Tool Called event.");
        }

        var conversation = AIConversationScope.Resolve();
        var input = AIConversationWorkflowInput.Build(conversation);
        input["ToolName"] = _name;
        input[AIConversationWorkflowInput.Arguments] = BuildArguments(arguments, resolution);

        WorkflowExecutionContext workflowContext;

        try
        {
            workflowContext = await services.GetRequiredService<IWorkflowManager>().StartWorkflowAsync(
                workflowType,
                startActivity,
                input,
                correlationId: conversation?.Session?.SessionId);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "The workflow behind AI tool '{ToolName}' failed.", _name);

            return Error("The request could not be completed.");
        }

        return ToToolResult(workflowContext);
    }

    /// <summary>
    /// Turns a finished workflow into the tool's answer.
    /// </summary>
    /// <param name="workflowContext">The workflow's execution context.</param>
    /// <returns>The answer for the model.</returns>
    internal static string ToToolResult(WorkflowExecutionContext workflowContext)
    {
        if (workflowContext is null)
        {
            return Error("The request could not be completed.");
        }

        if (workflowContext.Status == WorkflowStatus.Faulted)
        {
            return Error(string.IsNullOrWhiteSpace(workflowContext.Workflow?.FaultMessage)
                ? "The request could not be completed."
                : workflowContext.Workflow?.FaultMessage);
        }

        if (workflowContext.Status is WorkflowStatus.Halted or WorkflowStatus.Executing or WorkflowStatus.Resuming)
        {
            return JsonSerializer.Serialize(new
            {
                status = "accepted",
                message = "The request was accepted and is still being worked on. Let the customer know it is in hand.",
            });
        }

        if (workflowContext.Output is { Count: > 0 } output)
        {
            return JsonSerializer.Serialize(output);
        }

        return workflowContext.LastResult is null
            ? JsonSerializer.Serialize(new { status = "done" })
            : JsonSerializer.Serialize(new { status = "done", result = workflowContext.LastResult });
    }

    private Dictionary<string, object> BuildArguments(AIFunctionArguments arguments, AIToolParameterResolution resolution)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var resolved in resolution.Parameters)
        {
            values[resolved.Parameter.Name] = resolved.Value;
        }

        // The model's own arguments that no parameter claims, such as the free-text request.
        foreach (var (name, value) in arguments)
        {
            if (!values.ContainsKey(name))
            {
                values[name] = value is JsonElement element ? ToValue(element) : value;
            }
        }

        return values;
    }

    private static object ToValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText(),
        };

    private static JsonObject BuildBaseSchema(bool exposeRequest)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        if (exposeRequest)
        {
            properties[WorkflowToolConstants.RequestArgument] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "What is needed from this tool, in plain words, with every detail the customer gave.",
            };

            required.Add(WorkflowToolConstants.RequestArgument);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }

    private static string Error(string message)
        => JsonSerializer.Serialize(new { status = "error", message });
}
