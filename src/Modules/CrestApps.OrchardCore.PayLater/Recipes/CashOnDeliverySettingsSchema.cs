using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.PayLater.Recipes;

/// <summary>
/// Describes the <c>CashOnDeliverySettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class CashOnDeliverySettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(CashOnDeliverySettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The site settings of the Cash on delivery payment method.")
            .Properties(
                ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether Cash on delivery is offered, on checkouts of orders that are delivered.")),
                ("Instructions", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the buyer is told at checkout, for example which payments the courier takes.")),
                ("RequireSignedIn", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(false).Description("Whether only a signed-in buyer may use this method. Defaults to false.")),
                ("MaxOrderAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Minimum(0).Description("The largest order total that may use this method, or null for no limit.")))
            .AdditionalProperties(true);
}
