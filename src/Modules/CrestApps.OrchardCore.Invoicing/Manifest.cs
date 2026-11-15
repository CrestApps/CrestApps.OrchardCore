using CrestApps.OrchardCore;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Resources;
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

        // Every issued invoice is archived as a PDF, and sent and downloaded as one.
        BusinessDocumentsConstants.Features.Pdf,

        // The bill-to selector of the draft editor is the ItemSelector of the Resources feature.
        ResourcesConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = InvoicingConstants.Features.Estimates,
    Name = "Invoicing Estimates",
    Description = "Estimates (quotes) sent to customers, accepted or declined online, revised, expired, and turned into invoices or deposit invoices.",
    Category = "Commerce",
    Dependencies =
    [
        InvoicingConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = InvoicingConstants.Features.Billing,
    Name = "Invoicing Billing",
    Description = "Bill customers for work and costs: charges kept to bill later (expenses with a markup, fees), and with Time Tracker, the time people tracked at an hourly rate, put on draft invoices and released if the invoice is voided.",
    Category = "Commerce",
    Dependencies =
    [
        InvoicingConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = InvoicingConstants.Features.Api,
    Name = "Invoicing API",
    Description = "Lets back-office systems list, draft, issue, send and void invoices, record payments and download PDFs, and customers read their own invoices, over the commerce JSON API. Enable OpenAPI to document it.",
    Category = "Commerce",
    Dependencies =
    [
        InvoicingConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = InvoicingConstants.Features.Collections,
    Name = "Invoicing Collections",
    Description = "Late-fee policies that charge overdue invoices with debit notes linked to them, and customer statements.",
    Category = "Commerce",
    Dependencies =
    [
        InvoicingConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = InvoicingConstants.Features.Recurring,
    Name = "Invoicing Recurring",
    Description = "Recurring plans that make an invoice on a schedule, as a draft, issued, issued and sent, or issued and charged, with the period written into their lines.",
    Category = "Commerce",
    Dependencies =
    [
        InvoicingConstants.Features.Area,
    ]
)]
