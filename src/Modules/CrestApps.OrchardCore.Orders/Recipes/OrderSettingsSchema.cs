using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Orders.Recipes;

/// <summary>
/// Describes the <c>OrderSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class OrderSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(OrderSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How the store handles orders, under Settings › Commerce › Orders.")
            .Properties(
                ("SendConfirmations", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether a paid order's confirmation is emailed to the customer. It is sent only when the site has an email service.")),
                ("AllowQuoteRequests", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether signed-in buyers may ask for a quote from their cart instead of checking out.")),
                ("SendRecoveryEmails", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a buyer who leaves a checkout without paying is emailed once, with what they chose and a link back to it. It is sent only when the site has an email service.")),
                ("RecoveryDelayHours", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(1).Description("How many hours after the checkout was left the recovery email goes out.")),
                ("OrderBumpProductId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The product offered at checkout as an order bump: one tick adds it to the order before paying.")),
                ("OrderBumpPercentOff", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("How much less the order bump costs than the product's price, in percent, or null.")),
                ("OrderBumpHeadline", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the offer says above the product, such as \"Add a matching cap?\".")))
            .AdditionalProperties(true);
}
