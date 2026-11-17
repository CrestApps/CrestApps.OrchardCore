using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Describes the Payroll journal settings in Orchard Core's Settings recipe step.
/// </summary>
internal sealed class PayrollJournalSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(PayrollJournalSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
    {
        static JsonSchemaBuilder Account(string description)
            => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The ledger accounts of the payroll journal entries.")
            .Properties(
                (nameof(PayrollJournalSettings.NetPayAccount), Account("The account of net pay owed.")),
                (nameof(PayrollJournalSettings.WageExpenseAccount), Account("The expense account of earnings.")),
                (nameof(PayrollJournalSettings.DeductionLiabilityAccount), Account("The liability account of deductions.")),
                (nameof(PayrollJournalSettings.TaxLiabilityAccount), Account("The liability account of taxes.")),
                (nameof(PayrollJournalSettings.EmployerTaxExpenseAccount), Account("The expense account of the employer's taxes.")),
                (nameof(PayrollJournalSettings.ContributionExpenseAccount), Account("The expense account of employer contributions.")),
                (nameof(PayrollJournalSettings.ContributionLiabilityAccount), Account("The liability account of employer contributions.")),
                (nameof(PayrollJournalSettings.ReimbursementExpenseAccount), Account("The expense account of reimbursements.")),
                (nameof(PayrollJournalSettings.Mappings), new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Key", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A pay code's code or a tax code.")),
                        ("DebitAccount", Account("The account debited, or null for the default.")),
                        ("CreditAccount", Account("The account credited, or null for the default.")))
                    .Required("Key"))))
            .AdditionalProperties(true);
    }
}
