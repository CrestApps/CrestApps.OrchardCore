using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The send dialog of an invoice.
/// </summary>
public class InvoiceSendViewModel
{
    /// <summary>
    /// Gets or sets the editor of the page, built by the display drivers of this view model.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }

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
