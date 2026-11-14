using CrestApps.OrchardCore;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Transactions;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Invoicing",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Invoices and credit notes: drafts with line items and tax, issued with a number and posted to the Transactions ledger, paid online or by hand, voided, written off or credited.",
    Category = "Commerce"
)]

[assembly: Feature(
    Id = InvoicingConstants.Features.Area,
    Name = "Invoicing",
    Description = "Invoices and credit notes posted to the Transactions ledger, with payment terms, tax, numbering and printable business documents.",
    Category = "Commerce",
    Dependencies =
    [
        TransactionsConstants.Features.Area,
        TransactionsConstants.Features.PaymentTerms,
        BusinessDocumentsConstants.Features.Area,
    ]
)]
