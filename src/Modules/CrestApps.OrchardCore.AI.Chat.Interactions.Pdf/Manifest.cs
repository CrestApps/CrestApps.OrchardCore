using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Chat Interactions - Documents (PDF)",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Extends the 'AI Chat Interactions - Documents' feature by allowing PDF file.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.ChatDocuments,
    ]
)]
