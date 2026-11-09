using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Shipping.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Shipping.Recipes;

/// <summary>
/// Describes the <c>CarrierProviderSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class CarrierProviderSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(CarrierProviderSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Which carrier provider the store uses, on the Providers tab of Settings › Commerce › Shipping.")
            .Properties(
                ("DefaultProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The technical name of the default carrier provider, or null for none.")))
            .AdditionalProperties(true);
}
