using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SmsTask</c> workflow task.
/// </summary>
public sealed class SmsTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SmsTask";

    /// <inheritdoc />
    protected override string Category => "Messaging";

    /// <inheritdoc />
    protected override string DisplayText => "SMS Task";

    /// <inheritdoc />
    protected override string Description => "Sends an SMS message to a phone number using the configured SMS provider";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["PhoneNumber", "Body"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PhoneNumber", WorkflowActivitySchemaBuilders.LiquidExpression("The destination phone number. Must include the country code, for example +1 for the United States."));
        yield return ("Body", WorkflowActivitySchemaBuilders.LiquidExpression("The body of the SMS message."));
    }
}
