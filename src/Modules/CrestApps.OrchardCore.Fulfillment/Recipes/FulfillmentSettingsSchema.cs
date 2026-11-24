using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Fulfillment.Recipes;

/// <summary>
/// Describes the <c>FulfillmentSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class FulfillmentSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(FulfillmentSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How paid orders reach the fulfillment partners, under Settings › Commerce › Fulfillment Partners.")
            .Properties(
                ("SendAutomatically", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether paid orders are sent to the connected partners by themselves. When false, a person sends each order from its page.")))
            .AdditionalProperties(true);
}
