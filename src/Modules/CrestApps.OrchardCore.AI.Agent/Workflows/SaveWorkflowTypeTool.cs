using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Creates a workflow from a full definition, as the Create Workflow page creates one (a new workflow type ID, saved
/// through the workflow type store, which records its first version), with the activities and transitions the model
/// wrote. It generates the activity IDs, checks the definition first and reports what it saved, or why it saved
/// nothing. Replacing the definition of an existing workflow is not offered: the designer keeps those changes in a draft
/// that only the Workflows module can write.
/// </summary>
public sealed class SaveWorkflowTypeTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveWorkflowType";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The name of the new workflow."
        },
        "isEnabled": {
          "type": "boolean",
          "description": "Whether the workflow starts instances as soon as it is saved. Defaults to false; turn it on later with enableWorkflowType once the user confirmed."
        },
        "settings": {
          "type": "object",
          "description": "The execution settings. Every setting is optional.",
          "properties": {
            "instances": {
              "type": "string",
              "enum": [ "Any", "One", "OnePerCorrelation" ],
              "description": "How many instances may run at a time: any number, one, or one per correlation ID. Defaults to Any."
            },
            "lockTimeout": {
              "type": "integer",
              "description": "Milliseconds to wait for the lock of an instance before giving up; 0 means instances are not locked."
            },
            "lockExpiration": {
              "type": "integer",
              "description": "Milliseconds after which the lock of an instance expires; 0 means instances are not locked."
            },
            "deleteFinishedWorkflows": {
              "type": "boolean",
              "description": "Whether finished instances are deleted at once."
            },
            "usableAsActivity": {
              "type": "boolean",
              "description": "Whether other workflows can run this one as an activity."
            },
            "branchingMode": {
              "type": "string",
              "enum": [ "FirstOnly", "All" ],
              "description": "Whether only the first transition of an outcome is followed, or all of them. Defaults to FirstOnly."
            },
            "faultOnScriptErrors": {
              "type": "boolean",
              "description": "Whether a script error faults the instance instead of being logged."
            },
            "recordActivityData": {
              "type": "boolean",
              "description": "Whether the journal records what each activity evaluated and set. Defaults to true."
            }
          },
          "additionalProperties": false
        },
        "variables": {
          "type": "array",
          "description": "The variables the workflow declares.",
          "items": {
            "type": "object",
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
          }
        },
        "activities": {
          "type": "array",
          "description": "The activities. The tool generates their IDs; the transitions refer to them by alias.",
          "items": {
            "type": "object",
            "properties": {
              "alias": { "type": "string", "description": "A short name of your choice, unique in this call, that the transitions use." },
              "name": { "type": "string", "description": "The activity name from listWorkflowActivities." },
              "isStart": { "type": "boolean", "description": "Whether this event starts the workflow. Only events can start a workflow." },
              "x": { "type": "integer", "description": "The left position in the designer. Optional." },
              "y": { "type": "integer", "description": "The top position in the designer. Optional." },
              "properties": { "type": "object", "description": "The settings of the activity, matching the schema getWorkflowActivity returns." }
            },
            "required": [ "alias", "name" ],
            "additionalProperties": false
          }
        },
        "transitions": {
          "type": "array",
          "description": "The connections between activities.",
          "items": {
            "type": "object",
            "properties": {
              "source": { "type": "string", "description": "The alias of the activity the transition leaves." },
              "outcome": { "type": "string", "description": "The outcome of the source activity that the transition follows, from getWorkflowActivity." },
              "destination": { "type": "string", "description": "The alias of the activity that runs next." }
            },
            "required": [ "source", "outcome", "destination" ],
            "additionalProperties": false
          }
        },
        "validateOnly": {
          "type": "boolean",
          "description": "Check the definition and report the problems without saving anything."
        }
      },
      "required": [
        "name"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a workflow, disabled unless told otherwise, from a full definition: settings, variables, activities and transitions. It generates the activity IDs and checks the definition first; the result lists the problems to fix, and validateOnly checks a definition without saving it. It does not change existing workflows.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ManageWorkflows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadJsonObject(arguments, "settings", out var settings) ||
            !AgentToolArguments.TryReadJsonArray(arguments, "variables", out var variables) ||
            !AgentToolArguments.TryReadJsonArray(arguments, "activities", out var activities) ||
            !AgentToolArguments.TryReadJsonArray(arguments, "transitions", out var transitions))
        {
            return "Nothing was saved: settings must be an object, and variables, activities and transitions must be arrays.";
        }

        var store = services.GetRequiredService<IWorkflowTypeStore>();
        var schemaService = services.GetService<IWorkflowActivitySchemaService>();
        var builder = new WorkflowDefinitionBuilder(
            services.GetRequiredService<IActivityLibrary>(),
            schemaService is null ? [] : await schemaService.GetActivityDescriptorsAsync(cancellationToken),
            services.GetService<IWorkflowVariableTypeProvider>());

        var name = arguments.GetFirstValueOrDefault<string>("name");
        var workflowType = builder.Build(name, arguments.GetFirstValueOrDefault("isEnabled", false), settings, variables, activities, transitions);

        if (!string.IsNullOrEmpty(workflowType.Name) &&
            (await store.ListAsync()).Any(existing => string.Equals(existing.Name?.Trim(), workflowType.Name, StringComparison.OrdinalIgnoreCase)))
        {
            builder.Warnings.Add(new
            {
                activity = (string)null,
                message = $"Another workflow is already named '{workflowType.Name}'. Give this one another name so people can tell them apart.",
            });
        }

        if (builder.Errors.Count > 0 || arguments.GetFirstValueOrDefault("validateOnly", false))
        {
            return AgentToolResults.Json(new
            {
                saved = false,
                valid = builder.Errors.Count == 0,
                errors = builder.Errors,
                warnings = builder.Warnings,
                note = builder.Errors.Count == 0
                    ? "The definition passed the checks; nothing was saved because validateOnly was set."
                    : "Nothing was saved. Fix the errors and call saveWorkflowType again.",
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
            designerUrl = WorkflowToolSupport.GetDesignerPath(services, workflowType),
            note = workflowType.IsEnabled
                ? "The workflow is saved and enabled: its start events start instances from now on."
                : "The workflow is saved and disabled: nothing runs until enableWorkflowType turns it on. These checks are not the designer's own validation; open the designer to see its issues.",
        });
    }
}
