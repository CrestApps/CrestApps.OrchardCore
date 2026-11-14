using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using CrestApps.OrchardCore.Transactions.Core.Services;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Invoicing.Deployments;

/// <summary>
/// Exports every invoice, drafts and issued, with everything it holds.
/// </summary>
public sealed class InvoicesDeploymentStep : DeploymentStep
{
    /// <summary>
    /// The name of the step, the same in the deployment plan and in the recipe.
    /// </summary>
    public const string StepName = "Invoices";

    /// <summary>
    /// The name of the array the invoices are written to.
    /// </summary>
    public const string EntriesName = "Invoices";

    private static readonly LocalizationSource _category = LocalizationSource.Create<InvoicesDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<InvoicesDeploymentStep>("Invoices");

    /// <summary>
    /// Gets or sets whether the transactions the invoices posted are exported too, in a Transactions step written before
    /// the invoices, so an open invoice can be paid on the site it is imported into.
    /// </summary>
    public bool IncludeTransactions { get; set; } = true;

    /// <inheritdoc/>
    public override string Name => StepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

/// <summary>
/// Exports every credit note, drafts and issued, with everything it holds.
/// </summary>
public sealed class CreditNotesDeploymentStep : DeploymentStep
{
    /// <summary>
    /// The name of the step, the same in the deployment plan and in the recipe.
    /// </summary>
    public const string StepName = "CreditNotes";

    /// <summary>
    /// The name of the array the credit notes are written to.
    /// </summary>
    public const string EntriesName = "CreditNotes";

    private static readonly LocalizationSource _category = LocalizationSource.Create<CreditNotesDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<CreditNotesDeploymentStep>("Credit notes");

    /// <inheritdoc/>
    public override string Name => StepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

/// <summary>
/// Writes every invoice to the <see cref="InvoicesDeploymentStep"/>, a page at a time. Unlike configuration, a document
/// keeps when it was created and who made it: they are part of its record.
/// </summary>
internal sealed class InvoicesDeploymentSource : CatalogDeploymentSourceBase<InvoicesDeploymentStep, Invoice>
{
    private const int PageSize = 500;

    private readonly IInvoiceStore _invoices;
    private readonly TransactionLedgerExporter _ledger;

    public InvoicesDeploymentSource(IInvoiceStore invoices, TransactionLedgerExporter ledger)
        : base(InvoicesDeploymentStep.EntriesName)
    {
        _invoices = invoices;
        _ledger = ledger;
    }

    // The transactions come first, so a recipe imports them before the invoices that name them.
    protected override async Task ProcessAsync(InvoicesDeploymentStep step, DeploymentPlanResult result)
    {
        if (step.IncludeTransactions)
        {
            var transactionIds = (await GetEntriesAsync())
                .Where(invoice => invoice.Status != InvoiceStatus.Draft && !string.IsNullOrEmpty(invoice.TransactionId))
                .Select(invoice => invoice.TransactionId);

            result.Steps.Add(await _ledger.ExportAsync(transactionIds));
        }

        await base.ProcessAsync(step, result);
    }

    protected override async Task<IEnumerable<Invoice>> GetEntriesAsync()
    {
        var entries = new List<Invoice>();

        for (var page = 1; ; page++)
        {
            var result = await _invoices.PageAsync(page, PageSize, new InvoiceQuery());

            entries.AddRange(result.Entries);

            if (result.Entries.Count < PageSize)
            {
                return entries;
            }
        }
    }

    protected override JsonObject Export(Invoice entry)
        => InvoicingDocumentExport.Export(entry);
}

/// <summary>
/// Writes every credit note to the <see cref="CreditNotesDeploymentStep"/>, invoice by invoice.
/// </summary>
internal sealed class CreditNotesDeploymentSource : CatalogDeploymentSourceBase<CreditNotesDeploymentStep, CreditNote>
{
    private readonly ICreditNoteStore _creditNotes;

    public CreditNotesDeploymentSource(ICreditNoteStore creditNotes)
        : base(CreditNotesDeploymentStep.EntriesName)
    {
        _creditNotes = creditNotes;
    }

    protected override async Task<IEnumerable<CreditNote>> GetEntriesAsync()
        => await _creditNotes.GetAllAsync();

    protected override JsonObject Export(CreditNote entry)
        => InvoicingDocumentExport.Export(entry);
}

internal static class InvoicingDocumentExport
{
    // Everything but the storing site's document id.
    public static JsonObject Export(InvoicingDocument document)
    {
        var node = JsonSerializer.SerializeToNode(document, document.GetType(), CatalogDeploymentSerializer.Options)?.AsObject() ?? [];

        node.Remove(nameof(InvoicingDocument.Id));

        return node;
    }
}

internal sealed class InvoicesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(InvoicesDeploymentStep);

    protected override string DisplayText => "Invoices";

    protected override string Description => "Exports every invoice, drafts and issued, with its number, lines, totals and history, and optionally the transactions they posted.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        =>
        [
            (nameof(InvoicesDeploymentStep.IncludeTransactions), new JsonSchemaBuilder()
                .Type(SchemaValueType.Boolean)
                .Default(true)
                .Description("Whether the transactions the invoices posted are exported too, in a Transactions step before the invoices, so an open invoice can be paid where it is imported.")),
        ];
}

internal sealed class InvoicesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, InvoicesDeploymentStep>
{
    public override IDisplayResult Display(InvoicesDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("InvoicesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("InvoicesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(InvoicesDeploymentStep step, BuildEditorContext context)
        => Initialize<InvoicesDeploymentStepViewModel>("InvoicesDeploymentStep_Fields_Edit", model => model.IncludeTransactions = step.IncludeTransactions)
            .Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(InvoicesDeploymentStep step, UpdateEditorContext context)
    {
        var model = new InvoicesDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.IncludeTransactions);

        step.IncludeTransactions = model.IncludeTransactions;

        return Edit(step, context);
    }
}

/// <summary>
/// The editor of the Invoices deployment step.
/// </summary>
public class InvoicesDeploymentStepViewModel
{
    public bool IncludeTransactions { get; set; }
}

internal sealed class CreditNotesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(CreditNotesDeploymentStep);

    protected override string DisplayText => "Credit notes";

    protected override string Description => "Exports every credit note, drafts and issued, with its number, lines, totals and history.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
