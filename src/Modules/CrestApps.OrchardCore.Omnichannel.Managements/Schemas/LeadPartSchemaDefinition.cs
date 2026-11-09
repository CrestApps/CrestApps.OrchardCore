using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Schemas;

/// <summary>
/// Describes the <c>LeadPart</c> settings and content to CrestApps Recipes.
/// </summary>
internal sealed class LeadPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => OmnichannelConstants.ContentParts.Lead;

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(LeadPartSettings), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("How a lead of this type is converted.")
                    .Properties(
                        (nameof(LeadPartSettings.TargetContactContentType), new JsonSchemaBuilder()
                            .Type(SchemaValueType.String | SchemaValueType.Null)
                            .Description("The contact content type a lead of this type becomes when it is converted.")),
                        (nameof(LeadPartSettings.DefaultOpportunityContentType), new JsonSchemaBuilder()
                            .Type(SchemaValueType.String | SchemaValueType.Null)
                            .Description("The opportunity content type offered first when a conversion creates an opportunity.")))
                    .AdditionalProperties(true)))
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("A lead. Company, Source, ListName, Rating and Owner are content fields of the part, described by their field schemas. The conversion and scrub members are written by the site and are normally left out.")
            .Properties(
                (nameof(LeadPart.StatusId), NullableString("The ItemId of the lead's status (see the OmnichannelLeadStatus step). When it is empty or unknown, the default status (else the first open one) is used; a converted lead takes the converted status.")),
                (nameof(LeadPart.IsClosed), Flag("Whether the status is closed. The site sets it from the status whenever the lead is saved or imported.")),
                (nameof(LeadPart.IsConverted), Flag("Whether the lead was converted. A converted lead is always closed.")),
                (nameof(LeadPart.ConvertedUtc), NullableString("When the lead was converted, in UTC.")),
                (nameof(LeadPart.ConvertedById), NullableString("The user id of the user who converted the lead.")),
                (nameof(LeadPart.ConvertedByUsername), NullableString("The user name of the user who converted the lead.")),
                (nameof(LeadPart.ConvertedContactItemId), NullableString("The content item identifier of the contact the lead became.")),
                (nameof(LeadPart.ConvertedContactType), NullableString("The content type of the contact the lead became.")),
                (nameof(LeadPart.ConvertedAccountItemId), NullableString("The content item identifier of the account the contact joined.")),
                (nameof(LeadPart.ConvertedOpportunityItemId), NullableString("The content item identifier of the opportunity the conversion created.")),
                (nameof(LeadPart.LastScrubbedUtc), NullableString("When the lead's numbers were last checked against a do-not-call registry, in UTC.")),
                (nameof(LeadPart.Imports), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))
                    .Description("The imports that brought the lead in, recorded by the site.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder NullableString(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description(description);

    private static JsonSchemaBuilder Flag(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description(description);
}
