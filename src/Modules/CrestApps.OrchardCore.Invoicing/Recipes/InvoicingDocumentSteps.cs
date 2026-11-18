using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Deployments;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Invoicing.Recipes;

/// <summary>
/// Imports the invoices a recipe lists under <c>Invoices</c> (<see cref="InvoicingDocumentImporter"/>): an issued invoice
/// keeps its number and never posts to the ledger again, and one the site already issued is kept as it is.
/// </summary>
internal sealed class InvoicesStep : NamedRecipeStepHandler
{
    private readonly InvoicingDocumentImporter _importer;

    public InvoicesStep(InvoicingDocumentImporter importer)
        : base(InvoicesDeploymentStep.StepName)
    {
        _importer = importer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var result = await _importer.ImportInvoicesAsync(CatalogRecipeSections.Section(context.Step, InvoicesDeploymentStep.EntriesName).Select(entry => (JsonObject)entry.DeepClone()));

        foreach (var error in result.Errors)
        {
            context.Errors.Add(error);
        }
    }
}

/// <summary>
/// Imports the credit notes a recipe lists under <c>CreditNotes</c>, as <see cref="InvoicesStep"/> imports invoices.
/// </summary>
internal sealed class CreditNotesStep : NamedRecipeStepHandler
{
    private readonly InvoicingDocumentImporter _importer;

    public CreditNotesStep(InvoicingDocumentImporter importer)
        : base(CreditNotesDeploymentStep.StepName)
    {
        _importer = importer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var result = await _importer.ImportCreditNotesAsync(CatalogRecipeSections.Section(context.Step, CreditNotesDeploymentStep.EntriesName).Select(entry => (JsonObject)entry.DeepClone()));

        foreach (var error in result.Errors)
        {
            context.Errors.Add(error);
        }
    }
}

/// <summary>
/// Describes the Invoices and CreditNotes recipe steps to CrestApps Recipes.
/// </summary>
internal abstract class InvoicingDocumentRecipeStepSchemaBase : IRecipeStep
{
    private JsonSchema _cached;

    public abstract string Name { get; }

    protected abstract string EntriesName { get; }

    protected abstract string Description { get; }

    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetDocumentProperties();

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(Name).Description($"Recipe step discriminator. Must be '{Name}'.")),
                (EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties([.. CommonProperties(), .. GetDocumentProperties()])
                        .Required("ItemId")
                        .AdditionalProperties(true))
                    .Description(Description)))
            .Required("name", EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static IEnumerable<(string Name, JsonSchemaBuilder Schema)> CommonProperties()
    {
        yield return ("ItemId", Text("The document's 26-character id. A draft with this id is replaced; an issued document with this id is kept as it is."));
        yield return ("Number", Text("The document's number, kept as it is. Required on an issued document; refused when another document of the site holds it."));
        yield return ("BillTo", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("Who is billed: Party (Kind and Id), Contact (Name, Email, Phone, Address), TaxId, TaxTreatment, ExemptionReason and CopyTo."));
        yield return ("Issuer", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("The business that issued it, as copied at issue."));
        yield return ("Currency", Text("The three-letter currency code."));
        yield return ("PricesIncludeTax", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether line prices include tax."));
        yield return ("Lines", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object)).Description("The lines: Id, Kind (Item, Service, Discount, Text), Name, Description, Quantity, Unit, UnitPrice, DiscountPercent, DiscountAmount, TaxCategoryCode, Taxes, and the computed Net, TaxAmount and Total."));
        yield return ("Charges", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object)).Description("Document-level discounts and charges."));
        yield return ("Taxes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object)).Description("The tax lines, one per rate."));
        yield return ("Totals", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("Subtotal, DiscountTotal, ChargeTotal, TaxTotal, RoundingAmount and Total."));
        yield return ("IssueDate", Text("The issue date, yyyy-MM-dd."));
        yield return ("IssuedUtc", Text("When it was issued, in UTC (ISO 8601)."));
        yield return ("History", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object)).Description("The document's append-only history."));
        yield return ("CreatedUtc", Text("When it was created, in UTC (ISO 8601); kept."));
    }

    protected static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}

internal sealed class InvoicesRecipeStepSchema : InvoicingDocumentRecipeStepSchemaBase
{
    public override string Name => InvoicesDeploymentStep.StepName;

    protected override string EntriesName => InvoicesDeploymentStep.EntriesName;

    protected override string Description => "The invoices to import, as an Invoices deployment step writes them. Issued invoices keep their numbers and never post to the ledger again.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetDocumentProperties()
    {
        yield return ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("Draft, Open, Voided or Uncollectible (or its number)."));
        yield return ("Settlement", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("Unpaid, PartiallyPaid or Paid, mirrored from the transaction."));
        yield return ("DueDate", Text("The due date, yyyy-MM-dd."));
        yield return ("PaymentTermId", Text("The payment term's id."));
        yield return ("TransactionId", Text("The id of the transaction the invoice posted; the transaction travels with the Transactions step."));
        yield return ("AmountPaid", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("What was paid, mirrored from the transaction."));
        yield return ("AmountCredited", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("What credit notes took off."));
        yield return ("AmountDue", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("What is still owed."));
    }
}

internal sealed class CreditNotesRecipeStepSchema : InvoicingDocumentRecipeStepSchemaBase
{
    public override string Name => CreditNotesDeploymentStep.StepName;

    protected override string EntriesName => CreditNotesDeploymentStep.EntriesName;

    protected override string Description => "The credit notes to import, as a CreditNotes deployment step writes them. Import the invoices first.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetDocumentProperties()
    {
        yield return ("InvoiceId", Text("The id of the invoice it credits."));
        yield return ("InvoiceNumber", Text("The number of the invoice it credits."));
        yield return ("Reason", Text("Why the invoice was credited."));
        yield return ("Disposition", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("ReduceBalance, Refund or CustomerCredit (or its number)."));
        yield return ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("Draft, Issued or Voided (or its number)."));
    }
}
