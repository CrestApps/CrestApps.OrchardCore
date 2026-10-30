using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SetConversationVariablesTask</c> workflow activity.
/// </summary>
public sealed class SetConversationVariablesTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SetConversationVariablesTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Set Conversation Variables";

    /// <inheritdoc />
    protected override string Description => "Sets AI conversation variables, one name: value per line, which the profile instructions read as {{ Variables.name }}";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "No conversation"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Variables"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Variables", WorkflowActivitySchemaBuilders.LiquidExpression("The variables, one name: value per line. Liquid is supported; a name with no value clears the variable."));
    }
}
