using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>LogToCrmTask</c> workflow activity.
/// </summary>
public sealed class LogToCrmTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "LogToCrmTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Log to CRM";

    /// <inheritdoc />
    protected override string Description => "Finds or adds the customer in HubSpot or Salesforce by phone, updates mapped fields and logs the call";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Not found", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["ConnectionId", "Phone"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ConnectionId", WorkflowActivitySchemaBuilders.String("The id of a HubSpot or Salesforce integration connection."));
        yield return ("Phone", WorkflowActivitySchemaBuilders.LiquidExpression("The customer's phone number, which finds them."));
        yield return ("Fields", WorkflowActivitySchemaBuilders.LiquidExpression("'field: value' lines written to the contact. firstname, lastname and email are the contact's own fields."));
        yield return ("Title", WorkflowActivitySchemaBuilders.LiquidExpression("The logged call's title. Defaults to 'AI call'."));
        yield return ("Notes", WorkflowActivitySchemaBuilders.LiquidExpression("The logged call's notes, such as the summary and outcome."));
        yield return ("CreateIfMissing", WorkflowActivitySchemaBuilders.Boolean("Whether a customer who is not in the CRM is added. Defaults to true."));
    }
}
