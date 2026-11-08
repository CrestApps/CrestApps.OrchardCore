using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Recipes;

/// <summary>
/// Describes the <c>CustomerCheckoutSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class CustomerCheckoutSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(CustomerCheckoutSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("What an order's checkout asks the buyer, under Settings › Commerce › Customers.")
            .Properties(
                ("BillingAddress", new JsonSchemaBuilder().AnyOf(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Optional", "Off", "Required"), new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Default("Optional").Description("Whether the checkout asks for a billing address. A checkout keeps the setting it started with.")))
            .AdditionalProperties(true);
}
