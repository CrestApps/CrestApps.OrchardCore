using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>NotifyTask</c> workflow task.
/// </summary>
public sealed class NotifyTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "NotifyTask";

    /// <inheritdoc />
    protected override string Category => "UI";

    /// <inheritdoc />
    protected override string DisplayText => "Notify Task";

    /// <inheritdoc />
    protected override string Description => "Displays a notification message to the current user in the browser";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("NotificationType", WorkflowActivitySchemaBuilders.EnumValue("The notification type. Defaults to 'Success'.", "Success", "Information", "Warning", "Error"));
        yield return ("Message", WorkflowActivitySchemaBuilders.LiquidExpression("The message to show. The rendered value may contain HTML by design."));
    }
}
