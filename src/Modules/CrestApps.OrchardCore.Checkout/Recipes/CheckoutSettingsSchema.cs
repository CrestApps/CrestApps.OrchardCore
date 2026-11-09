using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Checkout.Recipes;

/// <summary>
/// Describes the <c>CheckoutSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class CheckoutSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(CheckoutSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The site-level settings that govern checkout behavior across every checkout scenario.")
            .Properties(
                ("DefaultPaymentMethod", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The key of the payment provider selected by default when a checkout offers more than one.")),
                ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Default("USD").Description("The default ISO-4217 currency used when a checkout does not specify one.")),
                ("SessionLifetimeHours", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(24).Description("How long an unpaid checkout is kept before it expires and whatever the provider was holding for it is released. Defaults to one day.")),
                ("Layout", new JsonSchemaBuilder().AnyOf(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Steps", "OnePage"), new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Description("How the checkout's steps are laid out: a page per step, or all on one page.")))
            .AdditionalProperties(true);
}
