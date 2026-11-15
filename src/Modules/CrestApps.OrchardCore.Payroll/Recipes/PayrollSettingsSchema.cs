using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Describes Payroll's site settings in Orchard Core's Settings recipe step.
/// </summary>
internal sealed class PayrollSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(PayrollSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Payroll's settings.")
            .Properties(
                (nameof(PayrollSettings.AllowPreparerToApprove), new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the person who prepared a pay run may approve it. Off by default.")),
                (nameof(PayrollSettings.AllowChangingOwnPay), new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether people may change their own pay through the admin pages. Off by default.")),
                (nameof(PayrollSettings.DefaultCountryPack), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The country pack of a new employer, such as US.")),
                (nameof(PayrollSettings.DefaultCurrency), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The currency of a new employer, such as USD.")),
                (nameof(PayrollSettings.MinimumHourlyWage), new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The least an hour a tipped person must earn with their tips, or null for no check.")),
                (nameof(PayrollSettings.MaximumTipCredit), new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The most an hour of tips counts toward the minimum wage.")))
            .AdditionalProperties(true);
}
