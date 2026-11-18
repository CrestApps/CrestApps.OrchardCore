using CrestApps.OrchardCore;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.Receipts.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Receipts",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides configurable, printable proof-of-purchase receipts that any module can reuse.",
    Category = "Commerce"
)]

[assembly: Feature(
    Id = ReceiptsConstants.Feature.Area,
    Name = "Receipts",
    Description = "Provides the reusable receipt builder, printable receipt view, and receipt branding settings. Receipts are printed as business documents.",
    Category = "Commerce",
    Dependencies =
    [
        BusinessDocumentsConstants.Features.Area,
    ]
)]
