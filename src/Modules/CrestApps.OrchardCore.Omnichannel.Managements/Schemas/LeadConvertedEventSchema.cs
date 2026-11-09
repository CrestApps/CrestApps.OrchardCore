using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Schemas;

/// <summary>
/// Describes the recipe schema of the <c>LeadConvertedEvent</c> workflow event.
/// </summary>
internal sealed class LeadConvertedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = nameof(LeadConvertedEvent);

    /// <inheritdoc />
    protected override string Category => "Omnichannel CRM";

    /// <inheritdoc />
    protected override string DisplayText => "Lead Converted";

    /// <inheritdoc />
    protected override string Description => "Starts when a lead is converted, from the Convert screen, a Convert Lead subject action or the Convert Lead task. The input carries the lead, the contact it became as ContentItem, and the identifiers of the account and opportunity";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
