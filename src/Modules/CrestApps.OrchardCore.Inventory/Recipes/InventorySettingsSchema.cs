using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Inventory.Recipes;

/// <summary>
/// Describes the <c>InventorySettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class InventorySettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(InventorySettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Settings › Commerce › Inventory.")
            .Properties(
                ("DefaultLowStockThreshold", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(5).Description("How few available units count as low stock for products without their own threshold.")),
                ("HoldMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(60).Description("How many minutes stock stays held for an order that hasn't been paid for. A payment that arrives later still sells the units, below zero if they were sold to someone else meanwhile.")),
                ("LowStockNotificationRoles", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The roles whose users are notified when a product runs low or sells out. Notifications need Orchard Core's Notifications feature.")),
                ("Locations", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(
                    ("LocationId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The location's identifier, which stock levels refer to. The default location is always present.")),
                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The location's name, such as Main warehouse.")),
                    ("Address", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Where the location is, on one line, for staff.")),
                    ("OwnerKind", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The kind of record that owns the location, such as Asset for a van, or null for the site's own.")),
                    ("OwnerId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The record that owns the location."))).AdditionalProperties(true)).Description("The places stock is kept, in the order orders take units from them. The default location, where stock counted before there were locations is, is always one of them; see GetLocations.")))
            .AdditionalProperties(true);
}
