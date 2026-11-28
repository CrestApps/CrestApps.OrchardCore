using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Builds a workflow from a list of operations (add an activity, connect an outcome, declare a variable, set the
/// execution settings) and creates it as the Create Workflow page does (a new workflow type ID, saved through the
/// workflow type store, which records its first version), disabled. It generates the activity IDs, checks the result
/// first and reports what it saved, or why it saved nothing. Changing an existing workflow is refused: the designer keeps
/// those changes in a draft that only the Workflows module's draft manager can write.
/// </summary>
public sealed class EditWorkflowDraftTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "editWorkflowDraft";

    private const string AddActivityOperation = "addActivity";
    private const string ConnectOperation = "connect";
    private const string AddVariableOperation = "addVariable";
    private const string SetSettingsOperation = "setSettings";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "workflowTypeId": {
          "type": "string",
          "description": "The workflow to change, from listWorkflowTypes. Changing an existing workflow is not possible from the chat on this site yet (the tool refuses and returns the designer link); leave it out to create a new workflow."
        },
        "revision": {
          "type": "string",
          "description": "The revision of the existing workflow's draft from the last read, needed only to change an existing workflow. Leave it out when creating a workflow."
        },
        "name": {
          "type": "string",
          "description": "The name of the new workflow."
        },
        "operations": {
          "type": "array",
          "minItems": 1,
          "description": "The changes, applied in order: addActivity (alias, activity, isStart, x, y, properties), connect (source, outcome, destination), addVariable (variable) and setSettings (settings).",
          "items": {
            "type": "object",
            "properties": {
              "op": {
                "type": "string",
                "enum": [ "addActivity", "connect", "addVariable", "setSettings" ],
                "description": "The operation."
              },
              "alias": { "type": "string", "description": "addActivity: a short name of your choice, unique in this call, that connect operations use." },
              "activity": { "type": "string", "description": "addActivity: the activity name from listWorkflowActivities." },
              "isStart": { "type": "boolean", "description": "addActivity: whether this event starts the workflow. Only events can start a workflow." },
              "x": { "type": "integer", "description": "addActivity: the left position in the designer. Optional." },
              "y": { "type": "integer", "description": "addActivity: the top position in the designer. Optional." },
              "properties": { "type": "object", "description": "addActivity: the settings of the activity, with exactly the names of the schema getWorkflowActivity returns." },
              "source": { "type": "string", "description": "connect: the alias of the activity the transition leaves." },
              "outcome": { "type": "string", "description": "connect: the outcome of the source activity the transition follows, from getWorkflowActivity." },
              "destination": { "type": "string", "description": "connect: the alias of the activity that runs next." },
              "variable": {
                "type": "object",
                "description": "addVariable: the variable the workflow declares.",
                "properties": {
                  "name": { "type": "string", "description": "The variable name." },
                  "type": { "type": "string", "description": "The variable type, for example string, number, boolean, dateTime, object, array or any. Defaults to any." },
                  "defaultValue": { "description": "The value the variable starts with." },
                  "description": { "type": "string", "description": "What the variable holds." },
                  "isInput": { "type": "boolean", "description": "Whether the variable is set from the input the workflow is started with." },
                  "isOutput": { "type": "boolean", "description": "Whether the variable is returned when the workflow ends." }
                },
                "required": [ "name" ],
                "additionalProperties": false
              },
              "settings": {
                "type": "object",
                "description": "setSettings: the execution settings. Every setting is optional.",
                "properties": {
                  "instances": { "type": "string", "enum": [ "Any", "One", "OnePerCorrelation" ], "description": "How many instances may run at a time: any number, one, or one per correlation ID. Defaults to Any." },
                  "lockTimeout": { "type": "integer", "description": "Milliseconds to wait for the lock of an instance before giving up; 0 means instances are not locked." },
                  "lockExpiration": { "type": "integer", "description": "Milliseconds after which the lock of an instance expires; 0 means instances are not locked." },
                  "deleteFinishedWorkflows": { "type": "boolean", "description": "Whether finished instances are deleted at once." },
                  "usableAsActivity": { "type": "boolean", "description": "Whether other workflows can run this one as an activity." },
                  "branchingMode": { "type": "string", "enum": [ "FirstOnly", "All" ], "description": "Whether only the first transition of an outcome is followed, or all of them. Defaults to FirstOnly." },
                  "faultOnScriptErrors": { "type": "boolean", "description": "Whether a script error faults the instance instead of being logged." },
                  "recordActivityData": { "type": "boolean", "description": "Whether the journal records what each activity evaluated and set. Defaults to true." }
                },
                "additionalProperties": false
              }
            },
            "required": [ "op" ],
            "additionalProperties": false
          }
        },
        "validateOnly": {
          "type": "boolean",
          "description": "Check the result and report the problems without saving anything."
        }
      },
      "required": [
        "operations"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Builds a new workflow from a list of operations (add activities, connect their outcomes, declare variables, set the execution settings) and saves it disabled, so nothing runs until setWorkflowTypeEnabled turns it on. It generates the activity IDs, refuses unknown activities, settings and outcomes with the closest valid names, and says which checks it could not run; validateOnly checks without saving. Changing an existing workflow is not possible yet: it returns the designer link.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ManageWorkflows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var store = services.GetRequiredService<IWorkflowTypeStore>();

        if (arguments.TryGetFirstString("workflowTypeId", out var workflowTypeId) && !string.IsNullOrWhiteSpace(workflowTypeId))
        {
            var existing = await WorkflowToolSupport.FindWorkflowTypeAsync(store, workflowTypeId);

            if (existing is null)
            {
                return $"Nothing was changed: there is no workflow '{workflowTypeId.Trim()}'. Use listWorkflowTypes to find it, or leave workflowTypeId out to create a new workflow.";
            }

            // The designer keeps the changes to a workflow in a draft that only the Workflows module's draft manager
            // (IWorkflowTypeDraftManager, in the OrchardCore.Workflows module assembly) writes and publishes. Saving the
            // live type directly would bypass the draft and the next publish would bring the old state back.
            return AgentToolResults.Json(new
            {
                saved = false,
                workflowTypeId = existing.WorkflowTypeId,
                name = existing.Name,
                designerUrl = WorkflowToolSupport.GetDesignerPath(services, existing),
                note = "Nothing was changed: an existing workflow is changed through its draft in the designer, which this site does not let a tool write yet. Open the designer link to make the change, or use cloneWorkflowType to start a copy and editWorkflowDraft without workflowTypeId to build a new workflow.",
            });
        }

        if (!AgentToolArguments.TryReadJsonArray(arguments, "operations", out var operations) || operations is null || operations.Count == 0)
        {
            return "Nothing was saved: give operations, the list of changes (addActivity, connect, addVariable, setSettings).";
        }

        var (settings, variables, activities, transitions, problems) = ReadOperations(operations);

        if (problems.Count > 0)
        {
            return AgentToolResults.Json(new
            {
                saved = false,
                valid = false,
                errors = problems.Select(problem => new { activity = (string)null, message = problem }).ToList(),
                note = "Nothing was saved. Fix the operations and call editWorkflowDraft again.",
            });
        }

        var schemaService = services.GetService<IWorkflowActivitySchemaService>();
        var builder = new WorkflowDefinitionBuilder(
            services.GetRequiredService<IActivityLibrary>(),
            schemaService is null ? [] : await schemaService.GetActivityDescriptorsAsync(cancellationToken),
            services.GetService<IWorkflowVariableTypeProvider>());

        var name = arguments.GetFirstValueOrDefault<string>("name");

        // A new workflow is always saved disabled: going live is the one step that asks for approval, in
        // setWorkflowTypeEnabled.
        var workflowType = builder.Build(name, isEnabled: false, settings, variables, activities, transitions);

        if (!string.IsNullOrEmpty(workflowType.Name) &&
            (await store.ListAsync()).Any(existing => string.Equals(existing.Name?.Trim(), workflowType.Name, StringComparison.OrdinalIgnoreCase)))
        {
            builder.Warnings.Add(new
            {
                activity = (string)null,
                message = $"Another workflow is already named '{workflowType.Name}'. Give this one another name so people can tell them apart.",
            });
        }

        var checks = new
        {
            activityNames = "checked",
            activitySettings = builder.UncheckedProperties == 0 ? "checkedAgainstSchemas" : "partlyChecked",
            outcomes = builder.UncheckedOutcomes == 0 ? "checked" : "partlyChecked",
            designerValidation = "notRun",
            expressions = "notChecked",
        };

        const string ChecksNote = "The designer's own validation of each activity's settings and the validation of Liquid and JavaScript expressions live in the Workflows module and did not run here: open the designer to see its issues before enabling the workflow.";

        if (builder.Errors.Count > 0 || arguments.GetFirstValueOrDefault("validateOnly", false))
        {
            return AgentToolResults.Json(new
            {
                saved = false,
                valid = builder.Errors.Count == 0,
                errors = builder.Errors,
                warnings = builder.Warnings,
                checks,
                note = builder.Errors.Count == 0
                    ? "The operations passed the checks; nothing was saved because validateOnly was set. " + ChecksNote
                    : "Nothing was saved. Fix the errors and call editWorkflowDraft again.",
            });
        }

        builder.AssignIds(
            workflowType,
            services.GetRequiredService<IWorkflowTypeIdGenerator>(),
            services.GetRequiredService<IActivityIdGenerator>());

        await store.SaveAsync(workflowType);

        return AgentToolResults.Json(new
        {
            saved = true,
            id = workflowType.Id,
            workflowTypeId = workflowType.WorkflowTypeId,
            name = workflowType.Name,
            isEnabled = workflowType.IsEnabled,
            versionId = workflowType.VersionId,
            activities = builder.Activities.Select(activity => new
            {
                alias = activity.Alias,
                activityId = activity.Record.ActivityId,
                name = activity.Record.Name,
                isStart = activity.Record.IsStart,
            }).ToList(),
            transitions = workflowType.Transitions.Count,
            warnings = builder.Warnings,
            checks,
            designerUrl = WorkflowToolSupport.GetDesignerPath(services, workflowType),
            note = "The workflow is saved and disabled: nothing runs until setWorkflowTypeEnabled turns it on. " + ChecksNote,
        });
    }

    // Turns the operations into the settings, variables, activities and transitions the definition builder reads.
    private static (JsonObject Settings, JsonArray Variables, JsonArray Activities, JsonArray Transitions, List<string> Problems) ReadOperations(JsonArray operations)
    {
        var settings = new JsonObject();
        var variables = new JsonArray();
        var activities = new JsonArray();
        var transitions = new JsonArray();
        var problems = new List<string>();

        for (var index = 0; index < operations.Count; index++)
        {
            var position = index + 1;

            if (operations[index] is not JsonObject operation)
            {
                problems.Add($"Operation {position} must be an object with op.");

                continue;
            }

            var op = operation["op"] is JsonValue opValue && opValue.TryGetValue<string>(out var text) ? text?.Trim() : null;

            switch (op)
            {
                case AddActivityOperation:
                    activities.Add(new JsonObject
                    {
                        ["alias"] = operation["alias"]?.DeepClone(),
                        ["name"] = operation["activity"]?.DeepClone(),
                        ["isStart"] = operation["isStart"]?.DeepClone(),
                        ["x"] = operation["x"]?.DeepClone(),
                        ["y"] = operation["y"]?.DeepClone(),
                        ["properties"] = operation["properties"]?.DeepClone(),
                    });
                    break;

                case ConnectOperation:
                    transitions.Add(new JsonObject
                    {
                        ["source"] = operation["source"]?.DeepClone(),
                        ["outcome"] = operation["outcome"]?.DeepClone(),
                        ["destination"] = operation["destination"]?.DeepClone(),
                    });
                    break;

                case AddVariableOperation:
                    if (operation["variable"] is not JsonObject variable)
                    {
                        problems.Add($"Operation {position} (addVariable) needs variable, an object with at least a name.");

                        break;
                    }

                    variables.Add(variable.DeepClone());
                    break;

                case SetSettingsOperation:
                    if (operation["settings"] is not JsonObject values)
                    {
                        problems.Add($"Operation {position} (setSettings) needs settings, an object.");

                        break;
                    }

                    foreach (var (key, value) in values)
                    {
                        settings[key] = value?.DeepClone();
                    }

                    break;

                default:
                    problems.Add(string.IsNullOrEmpty(op)
                        ? $"Operation {position} needs op: addActivity, connect, addVariable or setSettings."
                        : $"Operation {position} has the op '{op}', which is not available. The operations are addActivity, connect, addVariable and setSettings; updating, removing or disconnecting means changing an existing workflow, which is done in the designer.");
                    break;
            }
        }

        return (settings.Count == 0 ? null : settings, variables, activities, transitions, problems);
    }
}
