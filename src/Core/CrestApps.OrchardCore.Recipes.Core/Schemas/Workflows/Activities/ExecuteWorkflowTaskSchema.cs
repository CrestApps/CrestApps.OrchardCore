using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ExecuteWorkflowTask</c> workflow task.
/// </summary>
public sealed class ExecuteWorkflowTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ExecuteWorkflowTask";

    /// <inheritdoc />
    protected override string Category => "Primitives";

    /// <inheritdoc />
    protected override string DisplayText => "Execute Workflow Task";

    /// <inheritdoc />
    protected override string Description => "Runs another workflow type that is usable as an activity ('IsActivity': true and a 'StartedByWorkflowEvent' start), sets its input variables, and gets its output variables back as outputs";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["WorkflowTypeId"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("WorkflowTypeId", WorkflowActivitySchemaBuilders.String("The 'WorkflowTypeId' of the workflow type to run. It must be enabled and have 'IsActivity' set to true."));
        yield return ("Inputs", new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(WorkflowActivitySchemaBuilders.SyntaxExpression("The expression of the value of the input variable.", "JavaScript"))
            .Description("The values of the input variables of the workflow, by variable name. An expression without a syntax is JavaScript."));
        yield return ("WaitForCompletion", WorkflowActivitySchemaBuilders.Boolean("Whether the task waits for the workflow to finish and gets its outputs. Otherwise, it takes its 'Done' outcome once the workflow started. Defaults to true."));
        yield return ("Outputs", new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("Name", WorkflowActivitySchemaBuilders.String("The name of the output variable.")),
                    ("TypeName", WorkflowActivitySchemaBuilders.String("The type of the output variable.", WorkflowActivitySchemaBuilders.VariableTypeNames)),
                    ("Description", WorkflowActivitySchemaBuilders.String("The description of the output variable.")))
                .Required("Name")
                .AdditionalProperties(true))
            .Description("A copy of the output variables of the workflow, kept so the designer knows the outputs of the task. Bind them to variables with 'OutputBindings'."));
        yield return ("ChildWorkflowId", WorkflowActivitySchemaBuilders.String("Runtime state. The instance the task waits on; normally omitted from recipes."));
    }
}
