using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Workflows;

/// <summary>
/// Describes the recipe schema of the <c>SendTextMessageTask</c> workflow task.
/// </summary>
public sealed class SendTextMessageTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = nameof(SendTextMessageTask);

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Send Text Message";

    /// <inheritdoc />
    protected override string Description => "Sends a text message from one of the business's SMS numbers through the messaging workspace, so replies land in the conversation";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => [nameof(SendTextMessageTask.From), nameof(SendTextMessageTask.To), nameof(SendTextMessageTask.Body)];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return (nameof(SendTextMessageTask.From), WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the number to send from, one of the business's SMS numbers."));
        yield return (nameof(SendTextMessageTask.To), WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the customer's number."));
        yield return (nameof(SendTextMessageTask.Body), WorkflowActivitySchemaBuilders.String("The Liquid template of the message."));
        yield return (nameof(SendTextMessageTask.UserName), WorkflowActivitySchemaBuilders.String("The optional Liquid expression that resolves the user name or user id of the agent a new conversation belongs to. Without one, the text is a system send."));
    }
}
