using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Returns what a workflow needs to know of activity types before it uses them: the JSON schema of their
/// <c>Properties</c>, their outcomes, their outputs and the values they provide to later activities. The schemas come
/// from the workflow activity schema service, the outputs and values from the activities themselves.
/// </summary>
public sealed class GetWorkflowActivityTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getWorkflowActivity";

    private const int MaxNames = 10;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "names": {
          "type": "array",
          "minItems": 1,
          "maxItems": 10,
          "items": {
            "type": "string"
          },
          "description": "The activity names, exactly as listWorkflowActivities returns them, for example 'ContentPublishedEvent' or 'EmailTask'."
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
    public override string Description => "Returns, for up to 10 activity names, the JSON schema of the activity's Properties, its outcomes (and whether they depend on its settings), its outputs and the values it provides to later activities. Call it for every activity type before adding one to a workflow.";

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

        if (!AgentToolArguments.TryReadStrings(arguments, "names", out var names) || names is null || names.Count == 0)
        {
            return "Give 'names', the activity names from listWorkflowActivities.";
        }

        if (names.Count > MaxNames)
        {
            return $"Give at most {MaxNames} activity names at a time.";
        }

        var activityLibrary = services.GetRequiredService<IActivityLibrary>();
        var schemaService = services.GetService<IWorkflowActivitySchemaService>();
        var localizerFactory = services.GetService<IStringLocalizerFactory>();
        var descriptors = schemaService is null
            ? []
            : await schemaService.GetActivityDescriptorsAsync(cancellationToken);

        var activities = new List<object>();
        var unknown = new List<string>();

        foreach (var name in names)
        {
            var activity = activityLibrary.ListActivities().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

            if (activity is null)
            {
                unknown.Add(name);

                continue;
            }

            var descriptor = descriptors.FirstOrDefault(item => string.Equals(item.Name, activity.Name, StringComparison.Ordinal));

            activities.Add(new
            {
                name = activity.Name,
                title = descriptor?.DisplayText ?? activity.DisplayText?.Value,
                category = descriptor?.Category ?? activity.Category?.Value,
                kind = activity is IEvent ? "event" : "task",
                description = descriptor?.Description,
                outcomes = descriptor?.Outcomes,
                outcomesDependOnSettings = descriptor?.HasDynamicOutcomes,
                propertiesSchema = descriptor?.Properties is null ? null : JsonSerializer.SerializeToNode(descriptor.Properties.Build()),
                propertiesNote = descriptor?.Properties is null
                    ? "No schema is registered for this activity's Properties, so saveWorkflowType cannot check them. Use the property names of the activity's editor."
                    : null,
                outputs = (activity as IActivityOutputs)?.GetOutputs()
                    .Select(output => new
                    {
                        name = output.Name,
                        type = output.TypeName,
                        title = Localize(localizerFactory, output.DisplayName),
                        description = Localize(localizerFactory, output.Description),
                    })
                    .ToList(),
                providedValues = (activity as IActivityProvidedValues)?.GetProvidedValues()
                    .Select(value => new
                    {
                        source = value.Source.ToString(),
                        name = value.Name,
                        type = value.TypeName,
                        description = Localize(localizerFactory, value.Description),
                        members = value.Members.Count == 0
                            ? null
                            : value.Members.Select(member => new
                            {
                                name = member.Name,
                                type = member.TypeName,
                            }).ToList(),
                    })
                    .ToList(),
            });
        }

        if (activities.Count == 0)
        {
            return $"There is no activity named {string.Join(", ", unknown.Select(name => $"'{name}'"))} on this site. Call listWorkflowActivities to see the activity names.";
        }

        return AgentToolResults.Json(new
        {
            activities,
            unknown = unknown.Count == 0 ? null : unknown,
        });
    }

    private static string Localize(IStringLocalizerFactory localizerFactory, LocalizationSource source)
    {
        if (source is null)
        {
            return null;
        }

        return localizerFactory is null ? source.Value : localizerFactory.Localize(source).Value;
    }
}
