using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Receipts.Recipes;

/// <summary>
/// Describes the <c>ReceiptSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ReceiptSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(ReceiptSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The site settings that brand every receipt the tenant issues. These are merged into each built receipt document so consumers never embed issuer branding themselves.")
            .Properties(
                ("HeaderTitle", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The heading printed at the top of every receipt (for example \"Payment receipt\").")),
                ("BusinessName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The issuing business name. When empty, the site name is used as a fallback.")),
                ("LogoUrl", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The URL of the issuing business logo.")),
                ("BusinessAddress", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The issuing business postal address, printed as free-form text.")),
                ("ContactEmail", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The issuing business contact email address.")),
                ("ContactPhone", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The issuing business contact phone number.")),
                ("Website", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The issuing business website.")),
                ("FooterText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The footer text printed at the bottom of every receipt.")),
                ("ShowTestPaymentBadge", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether a test-payment badge is shown when a payment ran in a gateway test mode. Defaults to true.")))
            .AdditionalProperties(true);
}
