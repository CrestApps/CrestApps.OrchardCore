using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Schemas;

/// <summary>
/// Describes the recipe schema of the <c>ConvertLeadTask</c> workflow task.
/// </summary>
internal sealed class ConvertLeadTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = nameof(ConvertLeadTask);

    /// <inheritdoc />
    protected override string Category => "Omnichannel CRM";

    /// <inheritdoc />
    protected override string DisplayText => "Convert Lead";

    /// <inheritdoc />
    protected override string Description => "Converts a lead into a contact, optionally with an account and an opportunity, and moves or cancels the lead's open activities";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Converted", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return (nameof(ConvertLeadTask.LeadContentItemId), WorkflowActivitySchemaBuilders.String($"The Liquid expression that resolves the content item identifier of the lead. Defaults to '{ConvertLeadTask.DefaultLeadExpression}'."));
        yield return (nameof(ConvertLeadTask.AccountMode), WorkflowActivitySchemaBuilders.EnumValue(
            "Which account the contact joins: none, a new one named after the lead's company, an existing one, or Automatic (the single account matching the lead's company, else a new one when the lead has a company). Defaults to Automatic.",
            nameof(LeadConversionAccountMode.None),
            nameof(LeadConversionAccountMode.CreateNew),
            nameof(LeadConversionAccountMode.UseExisting),
            nameof(LeadConversionAccountMode.Automatic)));
        yield return (nameof(ConvertLeadTask.CreateOpportunity), WorkflowActivitySchemaBuilders.Boolean("Whether the conversion also creates an opportunity. Defaults to false."));
        yield return (nameof(ConvertLeadTask.OpportunityContentType), WorkflowActivitySchemaBuilders.String("The content type of the opportunity. Empty uses the lead type's default opportunity type."));
        yield return (nameof(ConvertLeadTask.OpenActivities), WorkflowActivitySchemaBuilders.EnumValue(
            "What happens to the lead's activities that are not finished: Move them to the contact, or Cancel them. Defaults to Move.",
            nameof(LeadOpenActivityMode.Move),
            nameof(LeadOpenActivityMode.Cancel)));
    }
}
