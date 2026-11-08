using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Subscriptions.Recipes;

/// <summary>
/// Describes the <c>InstallmentPlanSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class InstallmentPlanSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(InstallmentPlanSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The site settings for installment plans.")
            .Properties(
                ("RetryDays", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Description("How many days after a failed automatic charge each retry is made, in order. Defaults to one, three and five days. After the last retry fails, the payment is left for the customer to pay.")),
                ("DefaultCollectionMethod", new JsonSchemaBuilder().AnyOf(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("AutoCharge", "Invoice"), new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Default("AutoCharge").Description("The collection method a new plan starts with.")),
                ("CheckoutSources", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The transaction sources (Transaction.Source) whose balances a customer may choose to pay in installments at checkout. Empty, the default, offers installments only where a module offers them itself.")),
                ("CheckoutInstallmentCounts", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Description("The numbers of payments a customer may choose from at checkout, for example 3 and 6.")),
                ("CheckoutFrequency", new JsonSchemaBuilder().AnyOf(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Weekly", "BiWeekly", "Monthly", "Quarterly"), new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Default("Monthly").Description("How often the payments a customer chooses at checkout fall due.")),
                ("CheckoutMinimumAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The smallest balance that may be paid in installments at checkout. Zero allows any balance.")))
            .AdditionalProperties(true);
}
