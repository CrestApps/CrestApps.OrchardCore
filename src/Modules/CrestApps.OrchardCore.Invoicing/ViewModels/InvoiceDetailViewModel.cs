using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// A card of an invoice's page: the invoice, who it is billed to, and what the person viewing it may do.
/// </summary>
public class InvoiceDetailViewModel
{
    public Invoice Invoice { get; set; }

    public string BillToName { get; set; }

    public bool CanEdit { get; set; }

    public bool CanIssue { get; set; }

    public bool CanVoid { get; set; }

    public bool CanWriteOff { get; set; }

    public bool CanReinstate { get; set; }

    public bool CanDelete { get; set; }

    public bool CanOpenTransaction { get; set; }

    public bool LedgerMissing { get; set; }
}
