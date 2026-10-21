using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "StripeSettings" recipe step — imports the Stripe settings. Secrets are taken in clear text and
/// protected on import, are never exported, and keep their stored value when omitted. The account and webhook
/// identifiers describe the source site's connection to Stripe, so the step ignores them.
/// </summary>
public sealed class StripeSettingsRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "StripeSettings";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
    {
        var settings = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Stripe settings. A member the step carries replaces the stored one.")
            .Properties(
                ("IsLive", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the live Stripe keys are used. When false, the test keys are used.")),
                ("CheckoutMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("PaymentElements", "HostedCheckout").Description("The Stripe checkout integration mode. Kept for compatibility; the checkout always collects card details on the site with Payment Elements.")),
                ("LivePublishableKey", Key("The live publishable key.", "pk_live_")),
                ("LivePrivateSecret", Secret("The live secret key.", "sk_live_")),
                ("LiveWebhookSecret", Secret("The live webhook signing secret.", "whsec_")),
                ("TestPublishableKey", Key("The test publishable key.", "pk_test_")),
                ("TestPrivateSecret", Secret("The test secret key.", "sk_test_")),
                ("TestWebhookSecret", Secret("The test webhook signing secret.", "whsec_")))
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Settings", settings),
            ],
            ["Settings"]);
    }

    private static JsonSchemaBuilder Key(string description, string prefix)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description($"{description} It must start with '{prefix}', or the step fails and nothing is stored.");

    private static JsonSchemaBuilder Secret(string description, string prefix)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description($"{description} It must start with '{prefix}', or the step fails and nothing is stored. Secret: supply it in clear text and it is data-protected before it is stored. It is never exported, and when it is omitted or empty the destination keeps the value it already stores.");
}
