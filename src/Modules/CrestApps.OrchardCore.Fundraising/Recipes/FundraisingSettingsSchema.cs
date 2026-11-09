using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Fundraising.Recipes;

/// <summary>
/// Describes the <c>FundraisingSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class FundraisingSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(FundraisingSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Who the receipts are from: the organization donations go to and what the receipt says.")
            .Properties(
                ("OrganizationName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The name of the organization donations go to, printed on receipts.")),
                ("TaxId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The organization's tax or charity registration number, printed on receipts.")),
                ("ReceiptStatement", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Default("No goods or services were provided in exchange for this contribution.").Description("What the receipt says about the donation, such as that no goods or services were given in return.")),
                ("EmailReceipts", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether donors get their receipt by email when they give.")))
            .AdditionalProperties(true);
}
