using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Transactions;
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
