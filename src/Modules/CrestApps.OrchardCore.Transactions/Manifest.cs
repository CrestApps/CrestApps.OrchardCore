using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Users.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Transactions",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Transactions",
    Id = TransactionsConstants.Features.Area,
    Description = "Tracks, reports, and settles outstanding financial obligations from any payment provider, with customer statements, an administrator report, and reminders.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,

        // Registers the display-name provider the transaction list and the ledger commands use to name people.
        UsersConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Name = "Transaction Reminders",
    Id = TransactionsConstants.Features.Notification,
    Description = "Sends outstanding-payment reminders through the notification system so each reminder honors the owner's channel preference.",
    Category = "Commerce",
    Dependencies =
    [
        TransactionsConstants.Features.Area,
        "OrchardCore.Notifications",
    ]
)]

[assembly: Feature(
    Name = "Send Payment Receipts",
    Id = TransactionsConstants.Features.Receipts,
    Description = "Sends the customer a numbered receipt after every payment applied to a transaction, however it was paid, and a notice after every refund. Printable receipts and invoices are available whenever Transactions and Receipts are both enabled.",
    Category = "Commerce",
    Dependencies =
    [
        TransactionsConstants.Features.Area,
        ReceiptsConstants.Feature.Area,
        "OrchardCore.Notifications",
    ]
)]
