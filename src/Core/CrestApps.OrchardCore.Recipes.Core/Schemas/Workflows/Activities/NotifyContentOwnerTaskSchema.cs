using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>NotifyContentOwnerTask</c> workflow task.
/// </summary>
public sealed class NotifyContentOwnerTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "NotifyContentOwnerTask";

    /// <inheritdoc />
    protected override string Category => "Notifications";

    /// <inheritdoc />
    protected override string DisplayText => "Notify Content's Owner Task";

    /// <inheritdoc />
    protected override string Description => "Sends a notification message to the owner of the content item currently in context";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed", "Failed: no user found"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Subject"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Subject", WorkflowActivitySchemaBuilders.LiquidExpression("The subject line of the notification."));
        yield return ("Summary", WorkflowActivitySchemaBuilders.LiquidExpression("The HTML summary for the notification."));
        yield return ("TextBody", WorkflowActivitySchemaBuilders.LiquidExpression("The optional plain-text body of the notification. Does not support HTML."));
        yield return ("HtmlBody", WorkflowActivitySchemaBuilders.LiquidExpression("The HTML body of the notification. Only sent when the provider supports HTML."));
        yield return ("IsHtmlPreferred", WorkflowActivitySchemaBuilders.Boolean("When true, the notification provider uses the HTML body when it supports HTML. Defaults to false."));
    }
}
