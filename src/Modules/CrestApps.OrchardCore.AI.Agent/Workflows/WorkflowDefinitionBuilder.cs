using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Roles;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Reads the definition of a new workflow that the model wrote (settings, variables, activities named by aliases and
/// the transitions between them) and checks it before it is saved: activity names exist on the site, start activities
/// are events, each activity's properties match the schema registered for it, transitions connect existing aliases
/// with outcomes the activity produces, and variables have a known type. These checks are not the designer's own
/// validation, which lives in the Workflows module.
/// </summary>
internal sealed class WorkflowDefinitionBuilder
{
    private readonly IActivityLibrary _activityLibrary;
    private readonly IReadOnlyList<WorkflowActivityDescriptor> _descriptors;
    private readonly IWorkflowVariableTypeProvider _variableTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowDefinitionBuilder"/> class.
    /// </summary>
    /// <param name="activityLibrary">The activity library.</param>
    /// <param name="descriptors">The activity descriptors of the schema service.</param>
    /// <param name="variableTypes">The variable types.</param>
    public WorkflowDefinitionBuilder(IActivityLibrary activityLibrary, IReadOnlyList<WorkflowActivityDescriptor> descriptors, IWorkflowVariableTypeProvider variableTypes)
    {
        _activityLibrary = activityLibrary;
        _descriptors = descriptors ?? [];
        _variableTypes = variableTypes;
    }

    /// <summary>
    /// Gets the problems that stop the save.
    /// </summary>
    public List<object> Errors { get; } = [];

    /// <summary>
    /// Gets the problems that do not stop the save but should be fixed or checked.
    /// </summary>
    public List<object> Warnings { get; } = [];

    /// <summary>
    /// Gets the alias of each activity, in the order given.
    /// </summary>
    public List<(string Alias, ActivityRecord Record)> Activities { get; } = [];

    /// <summary>
    /// Gets how many activities have no registered schema, so their properties were not checked.
    /// </summary>
    public int UncheckedProperties { get; private set; }

    /// <summary>
    /// Gets how many transitions follow an outcome that could not be checked (outcomes that depend on the settings, or
    /// that are not registered).
    /// </summary>
    public int UncheckedOutcomes { get; private set; }

    /// <summary>
    /// Builds the workflow type. Its <see cref="WorkflowType.WorkflowTypeId"/> and activity IDs are not set yet: the
    /// aliases stand in for the activity IDs in <see cref="WorkflowType.Transitions"/> until <see cref="AssignIds"/>.
    /// </summary>
    /// <param name="name">The name of the workflow.</param>
    /// <param name="isEnabled">Whether the workflow starts instances.</param>
    /// <param name="settings">The execution settings, or <see langword="null"/>.</param>
    /// <param name="variables">The variables, or <see langword="null"/>.</param>
    /// <param name="activities">The activities, or <see langword="null"/>.</param>
    /// <param name="transitions">The transitions, or <see langword="null"/>.</param>
    /// <returns>The workflow type.</returns>
    public WorkflowType Build(string name, bool isEnabled, JsonObject settings, JsonArray variables, JsonArray activities, JsonArray transitions)
    {
        var workflowType = new WorkflowType
        {
            Name = name?.Trim(),
            IsEnabled = isEnabled,
            RecordActivityData = true,
        };

        if (string.IsNullOrEmpty(workflowType.Name))
        {
            Error(null, "Give the workflow a name.");
        }

        ReadSettings(workflowType, settings);
        workflowType.Variables = ReadVariables(variables);
        workflowType.Activities = ReadActivities(activities, workflowType.Variables);
        workflowType.Transitions = ReadTransitions(transitions, workflowType.BranchingMode);
        CheckReachability(workflowType);

        return workflowType;
    }

    /// <summary>
    /// Gives the workflow type and its activities their IDs, and points the transitions at the activity IDs.
    /// </summary>
    /// <param name="workflowType">The workflow type <see cref="Build"/> returned.</param>
    /// <param name="workflowTypeIdGenerator">The workflow type ID generator.</param>
    /// <param name="activityIdGenerator">The activity ID generator.</param>
    public void AssignIds(WorkflowType workflowType, IWorkflowTypeIdGenerator workflowTypeIdGenerator, IActivityIdGenerator activityIdGenerator)
    {
        workflowType.WorkflowTypeId = workflowTypeIdGenerator.GenerateUniqueId(workflowType);

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (alias, record) in Activities)
        {
            record.ActivityId = activityIdGenerator.GenerateUniqueId(record);
            ids[alias] = record.ActivityId;
        }

        foreach (var transition in workflowType.Transitions)
        {
            transition.SourceActivityId = ids[transition.SourceActivityId];
            transition.DestinationActivityId = ids[transition.DestinationActivityId];
        }
    }

    // The execution settings setSettings takes, as the tool's schema declares them.
    private static readonly string[] _settingNames =
    [
        "instances",
        "lockTimeout",
        "lockExpiration",
        "deleteFinishedWorkflows",
        "usableAsActivity",
        "branchingMode",
        "faultOnScriptErrors",
        "recordActivityData",
    ];

    private static readonly string[] _instanceChoices = ["Any", "One", "OnePerCorrelation"];

    private static readonly string[] _branchingChoices = ["FirstOnly", "All"];

    // Reads the execution settings. An unknown setting, a value outside its choices or a value of the wrong type is an
    // error with the valid names or choices, never dropped or replaced by the default.
    private void ReadSettings(WorkflowType workflowType, JsonObject settings)
    {
        if (settings is null)
        {
            return;
        }

        foreach (var (key, _) in settings)
        {
            if (!_settingNames.Contains(key, StringComparer.Ordinal))
            {
                Error(null, $"The setting '{key}' does not exist. The settings are: {string.Join(", ", _settingNames)}.");
            }
        }

        var instances = ReadChoice(settings, "instances", _instanceChoices) ?? "Any";

        workflowType.IsSingleton = instances == "One";
        workflowType.IsSingletonPerCorrelation = instances == "OnePerCorrelation";
        workflowType.LockTimeout = ReadSettingInt(settings, "lockTimeout") ?? 0;
        workflowType.LockExpiration = ReadSettingInt(settings, "lockExpiration") ?? 0;
        workflowType.DeleteFinishedWorkflows = ReadSettingBool(settings, "deleteFinishedWorkflows") ?? false;
        workflowType.IsActivity = ReadSettingBool(settings, "usableAsActivity") ?? false;
        workflowType.BranchingMode = ReadChoice(settings, "branchingMode", _branchingChoices) == "All"
            ? WorkflowBranchingMode.All
            : WorkflowBranchingMode.FirstOnly;
        workflowType.FaultOnScriptErrors = ReadSettingBool(settings, "faultOnScriptErrors") ?? false;
        workflowType.RecordActivityData = ReadSettingBool(settings, "recordActivityData") ?? true;
    }

    // The choice a setting names, written as the choice is (case aside), or null when the setting is not given. A value
    // that is not one of the choices is an error.
    private string ReadChoice(JsonObject settings, string key, string[] choices)
    {
        if (!settings.ContainsKey(key) || settings[key] is null)
        {
            return null;
        }

        var text = ReadString(settings, key);
        var choice = choices.FirstOrDefault(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase));

        if (choice is null)
        {
            Error(null, $"The setting '{key}' must be one of: {string.Join(", ", choices)}.");
        }

        return choice;
    }

    private int? ReadSettingInt(JsonObject settings, string key)
    {
        if (!settings.ContainsKey(key) || settings[key] is null)
        {
            return null;
        }

        var number = ReadInt(settings, key);

        if (number is null)
        {
            Error(null, $"The setting '{key}' must be a whole number of milliseconds.");

            return null;
        }

        return number;
    }

    private bool? ReadSettingBool(JsonObject settings, string key)
    {
        if (!settings.ContainsKey(key) || settings[key] is null)
        {
            return null;
        }

        var flag = ReadBool(settings, key);

        if (flag is null)
        {
            Error(null, $"The setting '{key}' must be true or false.");
        }

        return flag;
    }

    private List<WorkflowVariableDefinition> ReadVariables(JsonArray variables)
    {
        var result = new List<WorkflowVariableDefinition>();

        foreach (var node in variables ?? [])
        {
            if (node is not JsonObject variable || ReadString(variable, "name") is not { } name)
            {
                Error(null, "Every variable needs a name.");

                continue;
            }

            if (result.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                Error(null, $"The variable '{name}' is declared twice.");

                continue;
            }

            var typeName = ReadString(variable, "type") ?? "any";
            var type = _variableTypes?.List().FirstOrDefault(descriptor => string.Equals(descriptor.Name, typeName, StringComparison.OrdinalIgnoreCase));

            if (type is null)
            {
                Error(null, $"The variable '{name}' has the type '{typeName}', which does not exist. Use one of: {string.Join(", ", _variableTypes?.List().Select(descriptor => descriptor.Name) ?? [])}.");

                continue;
            }

            var defaultValue = variable["defaultValue"]?.DeepClone();

            if (defaultValue is not null && !type.Instance.TryCoerce(defaultValue, out _))
            {
                Error(null, $"The default value of the variable '{name}' is not a valid {type.Name}.");
            }

            result.Add(new WorkflowVariableDefinition
            {
                Name = name,
                TypeName = type.Name,
                DefaultValue = defaultValue,
                Description = ReadString(variable, "description"),
                IsInput = ReadBool(variable, "isInput") ?? false,
                IsOutput = ReadBool(variable, "isOutput") ?? false,
            });
        }

        return result;
    }

    private List<ActivityRecord> ReadActivities(JsonArray activities, IList<WorkflowVariableDefinition> variables)
    {
        var records = new List<ActivityRecord>();
        var index = 0;

        foreach (var node in activities ?? [])
        {
            var position = index++;

            if (node is not JsonObject activity || ReadString(activity, "alias") is not { } alias)
            {
                Error(null, "Every activity needs an alias, a short name of your choice that the transitions use.");

                continue;
            }

            if (Activities.Any(existing => existing.Alias == alias))
            {
                Error(alias, "Two activities have this alias. Give each activity its own alias.");

                continue;
            }

            var name = ReadString(activity, "name");
            var type = name is null ? null : _activityLibrary.ListActivities().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

            if (type is null)
            {
                var close = RoleToolSupport.FindCloseNames(name, _activityLibrary.ListActivities().Select(item => item.Name));

                Error(alias, name is null
                    ? "Give the activity name, from listWorkflowActivities."
                    : $"There is no activity named '{name}' on this site.{(close.Count > 0 ? $" Did you mean {string.Join(", ", close)}?" : string.Empty)} Use a name listWorkflowActivities returns.");

                continue;
            }

            var properties = activity["properties"] switch
            {
                null => new JsonObject(),
                JsonObject value => value.DeepClone().AsObject(),
                _ => null,
            };

            if (properties is null)
            {
                Error(alias, "The properties of an activity must be a JSON object.");

                continue;
            }

            var record = new ActivityRecord
            {
                Name = type.Name,
                IsStart = ReadBool(activity, "isStart") ?? false,
                X = ReadInt(activity, "x") ?? (100 + (position % 4 * 300)),
                Y = ReadInt(activity, "y") ?? (100 + (position / 4 * 200)),
                Properties = properties,
            };

            if (record.IsStart && type is not IEvent)
            {
                Error(alias, $"'{type.Name}' is a task, and only an event can start a workflow.");
            }

            CheckProperties(alias, record);
            CheckKnownPitfalls(alias, record, variables);

            Activities.Add((alias, record));
            records.Add(record);
        }

        if (records.Count > 0 && !records.Any(record => record.IsStart))
        {
            Warning(null, "This workflow has no start activity, so it never runs. Set isStart on the event that starts it.");
        }

        return records;
    }

    private void CheckProperties(string alias, ActivityRecord record)
    {
        var descriptor = _descriptors.FirstOrDefault(item => string.Equals(item.Name, record.Name, StringComparison.Ordinal));

        if (descriptor?.Properties is null)
        {
            UncheckedProperties++;
            Warning(alias, $"No schema is registered for '{record.Name}', so its properties were not checked.");

            return;
        }

        var schema = descriptor.Properties.Build();

        // A setting the schema does not declare is refused, with the closest declared names, unless the schema itself
        // allows other settings: a misspelled key would otherwise be stored and silently ignored by the activity.
        if (JsonSerializer.SerializeToNode(schema) is JsonObject schemaNode &&
            schemaNode["properties"] is JsonObject declared &&
            OnlyDeclaredPropertiesAllowed(schemaNode))
        {
            var unknown = record.Properties
                .Select(property => property.Key)
                .Where(key => !declared.ContainsKey(key))
                .ToList();

            foreach (var key in unknown)
            {
                var close = RoleToolSupport.FindCloseNames(key, declared.Select(property => property.Key));

                Error(alias, $"'{key}' is not a setting of '{record.Name}'.{(close.Count > 0 ? $" Did you mean {string.Join(", ", close)}?" : string.Empty)} Its settings are: {string.Join(", ", declared.Select(property => property.Key))}.");
            }

            if (unknown.Count > 0)
            {
                return;
            }
        }

        var results = schema.Evaluate(JsonSerializer.SerializeToElement(record.Properties), new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        });

        if (results.IsValid)
        {
            return;
        }

        var messages = (results.Details ?? [])
            .Prepend(results)
            .Where(detail => detail.Errors is not null)
            .SelectMany(detail => detail.Errors.Select(error => $"{(string.IsNullOrEmpty(detail.InstanceLocation.ToString()) ? "properties" : "properties" + detail.InstanceLocation)}: {error.Value}"))
            .Distinct()
            .Take(10)
            .ToList();

        Error(alias, $"The properties do not match the schema of '{record.Name}' (call getWorkflowActivity): {(messages.Count == 0 ? "they are not valid." : string.Join("; ", messages))}");
    }

    private void CheckKnownPitfalls(string alias, ActivityRecord record, IList<WorkflowVariableDefinition> variables)
    {
        if (record.Name == WorkflowToolSupport.HttpRequestEventName)
        {
            // The URL holds a token signed for the workflow and the activity, which only the designer can generate.
            record.Properties.Remove("Url");
            Warning(alias, "The URL of an HTTP Request event cannot be generated by a tool. Open the activity in the designer and generate its URL before you call it.");
        }

        if (record.Name == "ExecuteWorkflowTask" && string.IsNullOrWhiteSpace(ReadString(record.Properties, "WorkflowTypeId")))
        {
            Warning(alias, "Select the workflow this activity runs (WorkflowTypeId).");
        }

        if (record.Name == "SetVariableTask" &&
            ReadString(record.Properties, "VariableName") is { } variableName &&
            !variables.Any(variable => string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase)))
        {
            Warning(alias, $"'{variableName}' is not a declared variable: the value is stored as a workflow property, without a type.");
        }
    }

    private List<Transition> ReadTransitions(JsonArray transitions, WorkflowBranchingMode branchingMode)
    {
        var result = new List<Transition>();

        foreach (var node in transitions ?? [])
        {
            if (node is not JsonObject transition)
            {
                Error(null, "Every transition must be an object with source, outcome and destination.");

                continue;
            }

            var source = ReadString(transition, "source");
            var outcome = ReadString(transition, "outcome");
            var destination = ReadString(transition, "destination");
            var sourceActivity = Activities.FirstOrDefault(activity => activity.Alias == source);

            if (sourceActivity.Record is null || !Activities.Any(activity => activity.Alias == destination))
            {
                Error(source, $"A transition connects '{source}' to '{destination}', and one of them is not the alias of an activity of this workflow.");

                continue;
            }

            if (string.IsNullOrEmpty(outcome))
            {
                Error(source, $"The transition from '{source}' to '{destination}' needs the outcome it follows.");

                continue;
            }

            var descriptor = _descriptors.FirstOrDefault(item => item.Name == sourceActivity.Record.Name);

            if (descriptor is not null && descriptor.Outcomes.Count > 0 && !descriptor.HasDynamicOutcomes &&
                !descriptor.Outcomes.Contains(outcome, StringComparer.Ordinal))
            {
                var close = RoleToolSupport.FindCloseNames(outcome, descriptor.Outcomes);

                Error(source, $"'{sourceActivity.Record.Name}' has no outcome '{outcome}'.{(close.Count > 0 ? $" Did you mean {string.Join(", ", close)}?" : string.Empty)} Its outcomes are: {string.Join(", ", descriptor.Outcomes)}.");

                continue;
            }

            // An outcome that cannot be checked here is said so, never passed as checked.
            if (descriptor is null || descriptor.HasDynamicOutcomes || descriptor.Outcomes.Count == 0)
            {
                UncheckedOutcomes++;
                Warning(source, descriptor is not null && descriptor.HasDynamicOutcomes
                    ? $"The outcomes of '{sourceActivity.Record.Name}' depend on its settings, so the outcome '{outcome}' was not checked. Check the connection in the designer."
                    : $"No outcomes are registered for '{sourceActivity.Record.Name}', so the outcome '{outcome}' was not checked. Check the connection in the designer.");
            }

            if (result.Any(existing => existing.SourceActivityId == source && existing.SourceOutcomeName == outcome && existing.DestinationActivityId == destination))
            {
                continue;
            }

            if (branchingMode == WorkflowBranchingMode.FirstOnly && result.Any(existing => existing.SourceActivityId == source && existing.SourceOutcomeName == outcome))
            {
                Warning(source, $"The outcome '{outcome}' has more than one transition. Only the first one is followed unless branchingMode is All.");
            }

            result.Add(new Transition
            {
                SourceActivityId = source,
                SourceOutcomeName = outcome,
                DestinationActivityId = destination,
            });
        }

        return result;
    }

    private void CheckReachability(WorkflowType workflowType)
    {
        var starts = Activities.Where(activity => activity.Record.IsStart).Select(activity => activity.Alias).ToList();

        if (starts.Count == 0)
        {
            return;
        }

        var reachable = new HashSet<string>(starts, StringComparer.Ordinal);
        var pending = new Queue<string>(starts);
        var outgoing = workflowType.Transitions.ToLookup(transition => transition.SourceActivityId, StringComparer.Ordinal);

        while (pending.TryDequeue(out var alias))
        {
            foreach (var transition in outgoing[alias])
            {
                if (reachable.Add(transition.DestinationActivityId))
                {
                    pending.Enqueue(transition.DestinationActivityId);
                }
            }
        }

        foreach (var (alias, _) in Activities.Where(activity => !reachable.Contains(activity.Alias)))
        {
            Warning(alias, "This activity cannot be reached from a start activity, so it never runs.");
        }
    }

    // Whether a schema allows only the properties it declares: it says so with additionalProperties false, or says
    // nothing about other properties, which the tools read as "only these" so a misspelled setting is caught.
    private static bool OnlyDeclaredPropertiesAllowed(JsonObject schema)
    {
        if (schema.ContainsKey("patternProperties"))
        {
            return false;
        }

        return schema["additionalProperties"] switch
        {
            null => true,
            JsonValue value when value.TryGetValue<bool>(out var allowed) => !allowed,
            _ => false,
        };
    }

    private void Error(string alias, string message)
        => Errors.Add(new { activity = alias, message });

    private void Warning(string alias, string message)
        => Warnings.Add(new { activity = alias, message });

    private static string ReadString(JsonObject value, string key)
        => value[key] is JsonValue node && node.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;

    private static bool? ReadBool(JsonObject value, string key)
        => value[key] is JsonValue node && node.TryGetValue<bool>(out var flag) ? flag : null;

    private static int? ReadInt(JsonObject value, string key)
        => value[key] is JsonValue node && node.TryGetValue<int>(out var number) ? number : null;
}
