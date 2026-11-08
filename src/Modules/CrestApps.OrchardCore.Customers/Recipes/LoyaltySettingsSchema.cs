using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Recipes;

/// <summary>
/// Describes the <c>LoyaltySettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LoyaltySettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(LoyaltySettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How customers earn and spend loyalty points, under Settings > Commerce > Rewards.")
            .Properties(
                ("ProgramName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the program is called where buyers see it, such as \"Rewards\".")),
                ("PointsPerUnit", new JsonSchemaBuilder().Type(SchemaValueType.Number).Default(1).Description("The points earned for each unit of currency spent on goods, after discounts and before shipping and tax.")),
                ("PointValue", new JsonSchemaBuilder().Type(SchemaValueType.Number).Default(0.01).Description("What one point is worth when it's spent, in the checkout currency.")),
                ("MinimumPointsToRedeem", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(100).Description("The fewest points a buyer must have to spend them.")))
            .AdditionalProperties(true);
}
