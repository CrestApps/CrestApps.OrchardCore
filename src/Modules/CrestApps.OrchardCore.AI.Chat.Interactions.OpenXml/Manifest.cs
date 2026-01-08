using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
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
        AIConstants.Feature.ChatDocuments,
    ]
)]
