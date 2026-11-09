using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>TransferAICallTask</c> workflow task.
/// </summary>
public sealed class TransferAICallTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "TransferAICallTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Transfer AI Call";

    /// <inheritdoc />
    protected override string Description => "Transfers a live AI call to a phone number: cold, warm with a private briefing, or screened";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["PhoneNumber"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PhoneNumber", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the number to ring, with its country code."));
        yield return ("Mode", WorkflowActivitySchemaBuilders.EnumValue("How to transfer: cold, warm with a private briefing, or screened. Defaults to Warm.", "Cold", "Warm", "Screened"));
        yield return ("Briefing", WorkflowActivitySchemaBuilders.String("The Liquid template of what whoever answers hears before the caller is connected. Empty has the AI write it."));
        yield return ("Introduction", WorkflowActivitySchemaBuilders.String("The Liquid template of a line spoken once the two are connected."));
        yield return ("RingTimeoutSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(5).Maximum(120).Description("How long the number rings, in seconds. Defaults to 30."));
        yield return ("HoldAudio", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the URL or provider media name the caller hears while the number rings."));
        yield return ("ShowCallerNumber", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the person rung sees the caller's number. Defaults to false."));
        yield return ("ActivityId", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the call's activity. Defaults to {{ Workflow.Input.ActivityId }}."));
    }
}
