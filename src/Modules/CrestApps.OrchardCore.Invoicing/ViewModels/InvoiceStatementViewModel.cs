using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// Commerce > Statements: the customer, kind and period chosen, and the statement.
/// </summary>
public class InvoiceStatementViewModel
{
    public string Party { get; set; }

    public InvoiceStatementKind Kind { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public string Currency { get; set; }

    public string SendTo { get; set; }

    [BindNever]
    public string PartyName { get; set; }

    [BindNever]
    public string PartySearchEndpoint { get; set; }

    [BindNever]
    public bool CanPrint { get; set; }

    [BindNever]
    public InvoiceStatementRequest Request { get; set; }

    [BindNever]
    public InvoiceStatement Statement { get; set; }

    [BindNever]
    public BusinessDocument Document { get; set; }
}
