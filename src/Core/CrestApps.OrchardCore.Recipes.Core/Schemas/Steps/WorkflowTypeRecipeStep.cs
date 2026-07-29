using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "WorkflowType" recipe step — defines activities and transitions.
/// </summary>
public sealed class WorkflowTypeRecipeStep : IRecipeStep
{
    private readonly IWorkflowActivitySchemaService _activitySchemaService;

    private JsonSchema _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowTypeRecipeStep"/> class.
    /// </summary>
    /// <param name="activitySchemaService">The workflow activity schema service.</param>
    public WorkflowTypeRecipeStep(IWorkflowActivitySchemaService activitySchemaService)
    {
        _activitySchemaService = activitySchemaService;
    }

    public string Name => "WorkflowType";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var descriptors = await _activitySchemaService.GetActivityDescriptorsAsync(cancellationToken);
        var activitySchema = await _activitySchemaService.GetActivitySchemaAsync(cancellationToken);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("WorkflowType").Description("Recipe step discriminator. Must be 'WorkflowType'.")),
                ("data", WorkflowDataArray(activitySchema, descriptors).Description("Workflow type definitions to create or update.")))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }

    private static JsonSchemaBuilder WorkflowDataArray(
        JsonSchemaBuilder activitySchema,
        IReadOnlyList<WorkflowActivityDescriptor> descriptors)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("WorkflowTypeId", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Description("A stable unique identifier for the workflow type. Re-running the recipe with the same value replaces the existing workflow type instead of creating a duplicate.")),
                    ("Name", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Description("Workflow type name.")),
                    ("IsEnabled", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Boolean)
                        .Description("Whether the workflow type is enabled. Disabled workflow types never start.")),
                    ("IsSingleton", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Boolean)
                        .Description("Whether only a single instance of this workflow type can run at a time.")),
                    ("LockTimeout", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Integer)
                        .Description("The timeout in milliseconds to acquire a lock before resuming a workflow instance of this type. Only used when 'IsSingleton' is true.")),
                    ("LockExpiration", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Integer)
                        .Description("The expiration in milliseconds of the lock acquired before resuming a workflow instance of this type. Only used when 'IsSingleton' is true.")),
                    ("DeleteFinishedWorkflows", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Boolean)
                        .Description("Whether workflow instances are deleted once they complete.")),
                    ("Activities", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Array)
                        .Items(activitySchema)
                        .MinItems(1)
                        .Description("Activities that belong to the workflow type. Exactly one activity should set 'IsStart' to true.")),
                    ("Transitions", TransitionsSchema(descriptors)),
                    ("Properties", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .AdditionalProperties(true)
                        .Description("Free form property bag stored on the workflow type. Exported workflow types include it as an empty object unless a module persists data there.")))
                .Required("WorkflowTypeId", "Name", "Activities", "Transitions")
                .AdditionalProperties(true));
    }

    private static JsonSchemaBuilder TransitionsSchema(IReadOnlyList<WorkflowActivityDescriptor> descriptors)
    {
        var outcomeNames = descriptors
            .SelectMany(descriptor => descriptor.Outcomes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(outcome => outcome, StringComparer.Ordinal)
            .ToArray();
        var outcomeHint = outcomeNames.Length > 0
            ? $" Outcomes known to this tenant include: {string.Join(", ", outcomeNames)}."
            : string.Empty;

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("Id", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Integer)
                        .Description("Reserved for internal use. Exported workflow types always emit 0.")),
                    ("SourceActivityId", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Description("The 'ActivityId' of the activity the transition starts from.")),
                    ("SourceOutcomeName", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Description($"The outcome produced by the source activity that triggers this transition. Valid values are listed in the description of the source activity's 'Properties' schema.{outcomeHint}")),
                    ("DestinationActivityId", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Description("The 'ActivityId' of the activity the transition leads to.")))
                .Required("SourceActivityId", "SourceOutcomeName", "DestinationActivityId")
                .AdditionalProperties(true))
            .Description("Transition objects that connect activity outcomes to the next activity.");
    }
}
