using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>LiquidTask</c> workflow task.
/// </summary>
public sealed class LiquidTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "LiquidTask";

    /// <inheritdoc />
    protected override string Category => "Control Flow";

    /// <inheritdoc />
    protected override string DisplayText => "Liquid Task";

    /// <inheritdoc />
    protected override string Description => "Evaluates a Liquid expression and stores the result as the workflow last result";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Expression"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Expression", WorkflowActivitySchemaBuilders.LiquidExpression("The Liquid template to evaluate. The rendered value becomes the workflow last result."));
    }
}
