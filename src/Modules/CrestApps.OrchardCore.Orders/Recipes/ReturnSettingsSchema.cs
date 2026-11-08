using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Orders.Recipes;

/// <summary>
/// Describes the <c>ReturnSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ReturnSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(ReturnSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Settings › Commerce › Returns.")
            .Properties(
                ("WindowDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(30).Description("For how many days after an order is paid its buyer can ask to return items.")),
                ("Reasons", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The reasons a buyer chooses from when asking for a return. Empty offers the defaults: Doesn't fit, Not as described, Arrived damaged, Changed my mind and Other.")),
                ("AllowExchanges", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether a buyer can ask for something else instead of their money back.")),
                ("Instructions", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the buyer is told once the return is approved, such as where to send the items.")))
            .AdditionalProperties(true);
}
