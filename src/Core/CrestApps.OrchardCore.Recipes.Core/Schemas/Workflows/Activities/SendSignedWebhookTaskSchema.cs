using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SendSignedWebhookTask</c> workflow activity.
/// </summary>
public sealed class SendSignedWebhookTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SendSignedWebhookTask";

    /// <inheritdoc />
    protected override string Category => "HTTP";

    /// <inheritdoc />
    protected override string DisplayText => "Send Signed Webhook";

    /// <inheritdoc />
    protected override string Description => "Posts JSON to another system with an HMAC-SHA256 signature, retrying when the receiver is busy or fails";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Delivered", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Url"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Url", WorkflowActivitySchemaBuilders.LiquidExpression("The http or https address to post to."));
        yield return ("EventName", WorkflowActivitySchemaBuilders.LiquidExpression("Optionally, the event name sent in the X-Webhook-Event header."));
        yield return ("Body", WorkflowActivitySchemaBuilders.LiquidExpression("The JSON body. Defaults to {{ Workflow.Input | json }}."));
        yield return ("ProtectedSecret", WorkflowActivitySchemaBuilders.String("The signing secret, encrypted for this tenant. It can't be moved to another tenant; enter the secret again in the task's editor there."));
        yield return ("MaxAttempts", WorkflowActivitySchemaBuilders.Integer("How many times a delivery is tried, from 1 to 5. Defaults to 3."));
    }
}
