using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Composes the workflow activity schema from the activity library and the registered
/// <see cref="IWorkflowActivitySchemaDefinition"/> contributions.
/// </summary>
public sealed class WorkflowActivitySchemaService : IWorkflowActivitySchemaService
{
    private readonly IActivityLibrary _activityLibrary;
    private readonly IEnumerable<IWorkflowActivitySchemaDefinition> _definitions;

    private IReadOnlyList<WorkflowActivityDescriptor> _cachedDescriptors;
    private JsonSchemaBuilder _cachedActivitySchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowActivitySchemaService"/> class.
    /// </summary>
    /// <param name="activityLibrary">The workflow activity library.</param>
    /// <param name="definitions">The registered workflow activity schema definitions.</param>
    public WorkflowActivitySchemaService(
        IActivityLibrary activityLibrary,
        IEnumerable<IWorkflowActivitySchemaDefinition> definitions)
    {
        _activityLibrary = activityLibrary;
        _definitions = definitions;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WorkflowActivityDescriptor>> GetActivityDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedDescriptors is not null)
        {
            return _cachedDescriptors;
        }

        var definitions = _definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var descriptors = new List<WorkflowActivityDescriptor>();

        foreach (var activity in _activityLibrary.ListActivities().OrderBy(activity => activity.Name, StringComparer.Ordinal))
        {
            var context = new WorkflowActivitySchemaContext
            {
                ActivityName = activity.Name,
                IsEvent = activity is IEvent,
                IsTask = activity is ITask,
                Category = activity.Category?.Value,
                DisplayText = activity.DisplayText?.Value,
            };

            if (!definitions.TryGetValue(activity.Name, out var definition))
            {
                descriptors.Add(new WorkflowActivityDescriptor
                {
                    Name = context.ActivityName,
                    IsEvent = context.IsEvent,
                    IsTask = context.IsTask,
                    Category = context.Category,
                    DisplayText = context.DisplayText,
                    HasSchemaDefinition = false,
                });

                continue;
            }

            var schema = await definition.GetActivitySchemaAsync(context, cancellationToken);

            descriptors.Add(new WorkflowActivityDescriptor
            {
                Name = context.ActivityName,
                IsEvent = context.IsEvent,
                IsTask = context.IsTask,
                Category = schema?.Category ?? context.Category,
                DisplayText = schema?.DisplayText ?? context.DisplayText,
                Description = schema?.Description,
                Outcomes = schema?.Outcomes ?? [],
                HasDynamicOutcomes = schema?.HasDynamicOutcomes ?? false,
                HasSchemaDefinition = schema?.Properties is not null,
                Properties = schema?.Properties,
            });
        }

        _cachedDescriptors = descriptors;

        return _cachedDescriptors;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetActivitySchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedActivitySchema is not null)
        {
            return _cachedActivitySchema;
        }

        var descriptors = await GetActivityDescriptorsAsync(cancellationToken);

        var activityNames = descriptors
            .Select(descriptor => descriptor.Name)
            .ToArray();
        var eventNames = descriptors
            .Where(descriptor => descriptor.IsEvent)
            .Select(descriptor => descriptor.Name)
            .ToArray();

        var conditionals = new List<JsonSchemaBuilder>
        {
            new JsonSchemaBuilder()
                .If(new JsonSchemaBuilder()
                    .Properties(("IsStart", new JsonSchemaBuilder().Const(true)))
                    .Required("IsStart"))
                .Then(new JsonSchemaBuilder()
                    .Properties(("Name", new JsonSchemaBuilder()
                        .WithSuggestions(eventNames)
                        .Description("Start activities must be workflow events.")))),
        };

        foreach (var descriptor in descriptors.Where(descriptor => descriptor.Properties is not null))
        {
            conditionals.Add(new JsonSchemaBuilder()
                .If(new JsonSchemaBuilder()
                    .Properties(("Name", new JsonSchemaBuilder().Const(descriptor.Name)))
                    .Required("Name"))
                .Then(new JsonSchemaBuilder()
                    .Properties(("Properties", descriptor.Properties))));
        }

        _cachedActivitySchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ActivityId", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("A unique identifier for this activity within the workflow type. The recipe step never generates one, so it must be supplied. Referenced by 'Transitions[].SourceActivityId' and 'Transitions[].DestinationActivityId'.")),
                ("Name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(activityNames)
                    .Description("Activity type name. Start activities must be events. Events placed elsewhere in the workflow block execution until the matching event is triggered.")),
                ("IsStart", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether this activity is a workflow start event.")),
                ("X", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Description("Horizontal pixel position of the activity node in the designer.")),
                ("Y", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Description("Vertical pixel position of the activity node in the designer.")),
                ("Properties", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("Activity-specific property bag. The well known properties depend on the value of 'Name'. Additional members are accepted because modules can persist their own sections here.")))
            .Required("ActivityId", "Name", "Properties")
            .AdditionalProperties(true)
            .AllOf(conditionals.ToArray());

        return _cachedActivitySchema;
    }
}
