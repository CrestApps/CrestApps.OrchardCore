using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Subscriptions.Recipes;

/// <summary>
/// Describes the <c>SubscriptionSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class SubscriptionSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(SubscriptionSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Represents site-level settings for subscription checkout and billing.")
            .Properties(
                ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Default("USD").Description("The ISO currency code used for subscription invoices and payments.")),
                ("AllowGuestSignup", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether subscribers may complete checkout as guests.")),
                ("DunningGraceDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(7).Description("How many days a subscriber keeps access after a renewal payment fails.")))
            .AdditionalProperties(true);
}
