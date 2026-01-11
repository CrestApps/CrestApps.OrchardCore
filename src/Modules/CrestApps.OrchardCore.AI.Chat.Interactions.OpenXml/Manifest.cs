using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Chat Interactions - Documents (OpenXml)",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Extends the 'AI Chat Interactions - Documents' feature by allowing OpenXml file (i.e., .docx, .xlsx, .pptx).",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        // Don't add dependencies to the base Chat Interactions module to allow optional installation
        // and force the user to explicitly enable one of the Documents indexing module first.
    ]
)]
