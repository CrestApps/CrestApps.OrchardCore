using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "WorkflowType" recipe step — defines activities and transitions.
/// </summary>
public sealed class WorkflowTypeRecipeStep : IRecipeStep
{
    private readonly IActivityLibrary _library;
    private JsonSchema _cached;

    public WorkflowTypeRecipeStep(IActivityLibrary library) => _library = library;

    public string Name => "WorkflowType";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_cached is not null)
        {
            return ValueTask.FromResult(_cached);
        }

        var all = _library.ListActivities();
        var eventNames = all.Where(a => a is IEvent).Select(a => a.Name);
        var taskNames = all.Where(a => a is ITask).Select(a => a.Name);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("WorkflowType")),
                ("data", WorkflowDataArray(eventNames, taskNames)))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder WorkflowDataArray(
        IEnumerable<string> eventNames,
        IEnumerable<string> taskNames)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                    ("Activities", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Array)
                        .Items(ActivitySchema(eventNames, taskNames))),
                    ("Transitions", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Array)
                        .Items(new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .AdditionalProperties(true))))
                .Required("Name", "Activities", "Transitions")
                .AdditionalProperties(true));
    }

    private static JsonSchemaBuilder ActivitySchema(
        IEnumerable<string> eventNames,
        IEnumerable<string> taskNames)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("IsStart", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("X", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Number)
                    .Description("Horizontal pixel position of the activity node in the designer.")),
                ("Y", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Number)
                    .Description("Vertical pixel position of the activity node in the designer.")),
                ("Properties", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)))
            .Required("Name", "Properties")
            .AdditionalProperties(true)
            .If(new JsonSchemaBuilder()
                .Properties(("IsStart", new JsonSchemaBuilder().Const(true)))
                .Required("IsStart"))
            .Then(new JsonSchemaBuilder()
                .Properties(("Name", new JsonSchemaBuilder().Enum(eventNames))))
            .Else(new JsonSchemaBuilder()
                .Properties(("Name", new JsonSchemaBuilder().Enum(taskNames))));
    }
}
