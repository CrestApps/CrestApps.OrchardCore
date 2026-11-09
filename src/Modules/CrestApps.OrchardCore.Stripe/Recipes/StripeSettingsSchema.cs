using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Stripe.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Stripe.Recipes;

/// <summary>
/// Describes the <c>StripeSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class StripeSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(StripeSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Stores tenant-level Stripe configuration used by checkout and webhook processing.")
            .Properties(
                ("IsLive", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether live Stripe credentials are active.")),
                ("LivePublishableKey", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The live Stripe publishable key.")),
                ("LivePrivateSecret", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The live Stripe secret key. Use the StripeSettings recipe step to set it from a recipe. It is stored encrypted with this tenant's data protection keys, so a plain value set by the Settings step can't be read: set it in the admin.")),
                ("LiveWebhookSecret", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The live Stripe webhook signing secret. Use the StripeSettings recipe step to set it from a recipe. It is stored encrypted with this tenant's data protection keys, so a plain value set by the Settings step can't be read: set it in the admin.")),
                ("LiveAccountId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The identifier of the live Stripe account resolved when the live secret key was verified.")),
                ("LiveWebhookId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The identifier of the live webhook endpoint provisioned automatically when connecting.")),
                ("TestPublishableKey", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The test Stripe publishable key.")),
                ("TestPrivateSecret", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The test Stripe secret key. Use the StripeSettings recipe step to set it from a recipe. It is stored encrypted with this tenant's data protection keys, so a plain value set by the Settings step can't be read: set it in the admin.")),
                ("TestWebhookSecret", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The test Stripe webhook signing secret. Use the StripeSettings recipe step to set it from a recipe. It is stored encrypted with this tenant's data protection keys, so a plain value set by the Settings step can't be read: set it in the admin.")),
                ("TestAccountId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The identifier of the test Stripe account resolved when the test secret key was verified.")),
                ("TestWebhookId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The identifier of the test webhook endpoint provisioned automatically when connecting.")))
            .AdditionalProperties(true);
}
