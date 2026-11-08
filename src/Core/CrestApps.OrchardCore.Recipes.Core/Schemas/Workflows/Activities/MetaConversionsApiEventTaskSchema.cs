using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>MetaConversionsApiEventTask</c> workflow task.
/// </summary>
public sealed class MetaConversionsApiEventTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "MetaConversionsApiEventTask";

    /// <inheritdoc />
    protected override string Category => "Meta";

    /// <inheritdoc />
    protected override string DisplayText => "Meta Conversions API Event Task";

    /// <inheritdoc />
    protected override string Description => "Sends a server-side event to the Meta Conversions API, for example to report a completed order or a captured lead. Requires the Conversions API access token in the FacebookPixelSettings";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["EventName"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("EventName", WorkflowActivitySchemaBuilders.LiquidExpression("The name of the event, for example 'Purchase' or 'Lead'."));
        yield return ("EventSourceUrl", WorkflowActivitySchemaBuilders.LiquidExpression("The URL of the page where the event happened."));
        yield return ("ActionSource", WorkflowActivitySchemaBuilders.EnumValue("Where the conversion happened. Defaults to 'Website'.", "Website", "Email", "App", "PhoneCall", "Chat", "PhysicalStore", "SystemGenerated", "Other"));
        yield return ("EventId", WorkflowActivitySchemaBuilders.LiquidExpression("The identifier of the event, used to deduplicate it with the same event sent by the browser-side Meta Pixel."));
    }
}
