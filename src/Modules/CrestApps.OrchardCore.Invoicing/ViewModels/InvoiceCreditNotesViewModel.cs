using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The credit notes card of an invoice's page.
/// </summary>
public class InvoiceCreditNotesViewModel
{
    public Invoice Invoice { get; set; }

    public IList<CreditNote> CreditNotes { get; set; } = [];

    public bool CanCredit { get; set; }
}
