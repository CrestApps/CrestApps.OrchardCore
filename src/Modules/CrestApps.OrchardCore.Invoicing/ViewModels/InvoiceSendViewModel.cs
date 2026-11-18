using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The send dialog of an invoice.
/// </summary>
public class InvoiceSendViewModel
{
    public string ItemId { get; set; }

    public string To { get; set; }

    public string Cc { get; set; }

    public string Bcc { get; set; }

    [BindNever]
    public string Number { get; set; }

    [BindNever]
    public bool IssuesFirst { get; set; }

    [BindNever]
    public bool CanSend { get; set; }
}
