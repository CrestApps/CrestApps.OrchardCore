using CrestApps.OrchardCore;
using CrestApps.OrchardCore.BusinessDocuments;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Business Documents",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Printable business documents, such as receipts, payment requests, invoices, estimates and credit notes, rendered the same way by every module that issues one.",
    Category = "Commerce"
)]

[assembly: Feature(
    Id = BusinessDocumentsConstants.Features.Area,
    Name = "Business Documents",
    Description = "One model, template catalog, e-mail-safe HTML renderer and themeable shape for printable business documents, and the settings page of the number series they are numbered with.",
    Category = "Commerce"
)]

[assembly: Feature(
    Id = BusinessDocumentsConstants.Features.Pdf,
    Name = "Business Documents PDF",
    Description = "Renders business documents as PDF files, with the template's logo, accent color and paper size, page numbers and a watermark.",
    Category = "Commerce",
    Dependencies =
    [
        BusinessDocumentsConstants.Features.Area,
    ]
)]
