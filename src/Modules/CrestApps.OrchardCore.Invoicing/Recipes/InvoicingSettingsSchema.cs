using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Invoicing.Recipes;

/// <summary>
/// Describes <see cref="InvoicingSettings"/> for the Settings recipe step.
/// </summary>
internal sealed class InvoicingSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(InvoicingSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("What a new invoice starts with: its payment term, currency, texts and whether customers may pay part of it.")
            .Properties(
                ("DefaultTemplateId", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The document template invoices are printed with when they name none.")),
                ("DefaultPaymentTermId", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The payment term a new invoice uses when its customer has none. Empty means due on issue.")),
                ("DefaultCurrency", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The ISO 4217 currency code of a new invoice when its customer has none, such as USD.")),
                ("DefaultNotes", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("Notes printed on an invoice that has none.")),
                ("DefaultTerms", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("Terms printed on an invoice that has none.")),
                ("DefaultFooter", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The footer printed on an invoice that has none.")),
                ("PricesIncludeTax", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(false).Description("Whether a new invoice's line prices include tax.")),
                ("DefaultTaxTreatment", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(0).Description("The tax treatment of a new invoice's bill-to: 0 taxable, 1 exempt, 2 zero-rated, 3 reverse charge, 4 out of scope.")),
                ("AllowPartialPayment", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(false).Description("Whether customers may pay part of an invoice from its pay link.")),
                ("IssueOnSend", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether sending a draft issues it first.")),
                ("ArchivePdfOnIssue", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether the PDF of an invoice is stored when it is issued.")),
                ("CashRoundingIncrement", new JsonSchemaBuilder().Type(SchemaValueType.Number, SchemaValueType.Null).Description("The step totals are rounded to for cash, such as 0.05, or null for none.")),
                ("ShareLinkDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(60).Description("How many days the link that shows an invoice without signing in keeps working.")))
            .AdditionalProperties(true);
}
