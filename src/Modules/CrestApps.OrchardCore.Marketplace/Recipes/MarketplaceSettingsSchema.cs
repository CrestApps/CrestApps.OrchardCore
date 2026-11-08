using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Marketplace.Recipes;

/// <summary>
/// Describes the <c>MarketplaceSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class MarketplaceSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(MarketplaceSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How the marketplace takes its commission and when it releases what vendors earn.")
            .Properties(
                ("CommissionPercent", new JsonSchemaBuilder().Type(SchemaValueType.Number).Default(10).Description("The percent the store keeps of each vendor's sale, unless the vendor has its own.")),
                ("HoldDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(7).Description("How many days a sale is held in escrow after it's paid, such as for returns.")),
                ("ReleaseOnFulfillment", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether a sale of goods is also held until its order is fulfilled.")),
                ("AutomaticPayouts", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether released earnings are paid out by themselves, rather than when the store chooses.")),
                ("PayoutProvider", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The name of the payout provider vendors are paid through, or null to use the only one enabled. When several are enabled and none is named, nothing is paid out.")),
                ("DefaultCountry", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Default("US").Description("The country a vendor's Stripe account is made in when the vendor names none.")))
            .AdditionalProperties(true);
}
