using CrestApps.OrchardCore;
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
    Category = "Commerce"
)]
