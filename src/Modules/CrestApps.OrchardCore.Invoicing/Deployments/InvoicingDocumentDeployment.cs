using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using OrchardCore.Deployment;
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

    public InvoicesDeploymentSource(IInvoiceStore invoices)
        : base(InvoicesDeploymentStep.EntriesName)
    {
        _invoices = invoices;
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

    protected override string Description => "Exports every invoice, drafts and issued, with its number, lines, totals and history.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class CreditNotesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(CreditNotesDeploymentStep);

    protected override string DisplayText => "Credit notes";

    protected override string Description => "Exports every credit note, drafts and issued, with its number, lines, totals and history.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
