using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Recipes;

/// <summary>
/// Describes the <c>CustomerPrivacySettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class CustomerPrivacySettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(CustomerPrivacySettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How long the store keeps guests' personal data, under Settings › Commerce › Customers. Zero keeps it.")
            .Properties(
                ("InactiveGuestMonths", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(24).Description("The months after a guest customer's last activity when their personal data is erased.")),
                ("GuestOrderMonths", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(12).Description("The months after a guest's order was placed when its name, email, phone and billing address are removed. The lines, totals and dates stay.")))
            .AdditionalProperties(true);
}
