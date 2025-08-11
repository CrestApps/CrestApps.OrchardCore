using Json.Schema;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class WorkflowTypeSchemaStep : IRecipeStep
{
    private readonly IActivityLibrary _activityLibrary;

    private JsonSchema _schema;

    public WorkflowTypeSchemaStep(IActivityLibrary activityLibrary)
    {
        _activityLibrary = activityLibrary;
    }

    public string Name => "WorkflowType";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_schema != null)
        {
            return new ValueTask<JsonSchema>(_schema);
        }

        var activities = _activityLibrary.ListActivities();

        // Partition activities into "events" and "tasks" (adjust the predicates as needed).
        var events = activities.Where(x => x is IEvent).ToArray();
        var tasks = activities.Where(x => x is ITask).ToArray();

        var builder = new JsonSchemaBuilder();

        builder
           .Type(SchemaValueType.Object)
           .Properties(
               ("name", new JsonSchemaBuilder()
                   .Type(SchemaValueType.String)
                   .Const("WorkflowType")
               ),
               ("data", new JsonSchemaBuilder()
                   .Type(SchemaValueType.Array)
                   .Items(
                       new JsonSchemaBuilder()
                           .Type(SchemaValueType.Object)
                           .Properties(
                               ("Name", new JsonSchemaBuilder()
                                   .Type(SchemaValueType.String)
                               ),

                               // Activities array
                               ("Activities", new JsonSchemaBuilder()
                                   .Type(SchemaValueType.Array)
                                   .Items(
                                       // Activity object schema (we place the conditional on this object)
                                       new JsonSchemaBuilder()
                                           .Type(SchemaValueType.Object)
                                           .Properties(
                                               // base Name property (type only). The conditional below will add enum constraints.
                                               ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                               // IsStart property. If missing, the `if` won't match (treated as false via else).
                                               ("IsStart", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),

                                               // X and Y with descriptions
                                               ("X", new JsonSchemaBuilder()
                                                   .Type(SchemaValueType.Number)
                                                   .Description("The horizontal position (in pixels) where the workflow activity node appears on the UI.")
                                               ),
                                               ("Y", new JsonSchemaBuilder()
                                                   .Type(SchemaValueType.Number)
                                                   .Description("The vertical position (in pixels) where the workflow activity node appears on the UI.")
                                               ),

                                               // Properties object (arbitrary)
                                               ("Properties", new JsonSchemaBuilder()
                                                   .Type(SchemaValueType.Object)
                                                   .AdditionalProperties(true)
                                               )
                                           )
                                           .Required("Name", "Properties")
                                           .AdditionalProperties(true)

                                           // Conditional: if IsStart === true then restrict Name enum to event names,
                                           // otherwise (else) restrict Name enum to task names.
                                           .If(
                                               new JsonSchemaBuilder()
                                                   .Properties(("IsStart", new JsonSchemaBuilder().Const(true)))
                                                   .Required("IsStart")
                                           )
                                           .Then(
                                               new JsonSchemaBuilder()
                                                   .Properties(("Name", new JsonSchemaBuilder().Enum(events.Select(evt => evt.Name))))
                                           )
                                           .Else(
                                               new JsonSchemaBuilder()
                                                   .Properties(("Name", new JsonSchemaBuilder().Enum(tasks.Select(task => task.Name))))
                                           )
                                   )
                               ),

                               ("Transitions", new JsonSchemaBuilder()
                                   .Type(SchemaValueType.Array)
                                   .Items(
                                       new JsonSchemaBuilder()
                                           .Type(SchemaValueType.Object)
                                           .AdditionalProperties(true)
                                   )
                               )
                           )
                           .Required("Name", "Activities", "Transitions")
                           .AdditionalProperties(true)
                   )
               )
           )
           .Required("name", "data")
           .AdditionalProperties(true);

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
