using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The page of a credit note.
/// </summary>
public class CreditNoteDetailViewModel
{
    public CreditNote CreditNote { get; set; }

    public bool CanEdit { get; set; }

    public bool CanIssue { get; set; }

    public bool CanViewInvoice { get; set; }
}
