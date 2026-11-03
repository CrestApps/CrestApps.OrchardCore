using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>CreateAIActivityTask</c> workflow task.
/// </summary>
public sealed class CreateAIActivityTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "CreateAIActivityTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Create AI Activity";

    /// <inheritdoc />
    protected override string Description => "Creates an automated AI phone call or text for a customer, placed by the automated activities pass";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["AIProfileId", "Destination"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Channel", WorkflowActivitySchemaBuilders.StringEnum("Phone for a call, SMS for a text.", "Phone", "SMS"));
        yield return ("AIProfileId", WorkflowActivitySchemaBuilders.String("The identifier of the AI profile that holds the conversation."));
        yield return ("ChannelEndpointId", WorkflowActivitySchemaBuilders.String("Optional. The identifier of the number the call or text comes from."));
        yield return ("Destination", WorkflowActivitySchemaBuilders.LiquidExpression("The customer's phone number."));
        yield return ("ContactContentItemId", WorkflowActivitySchemaBuilders.LiquidExpression("Optional. The contact's content item identifier."));
        yield return ("SubjectContentType", WorkflowActivitySchemaBuilders.String("Optional. The subject content type the activity is about."));
        yield return ("CampaignId", WorkflowActivitySchemaBuilders.String("Optional. The campaign the activity belongs to."));
        yield return ("Variables", WorkflowActivitySchemaBuilders.LiquidExpression("Optional. The variables the conversation starts with, one name: value per line."));
    }
}
