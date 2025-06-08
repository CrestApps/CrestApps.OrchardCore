using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Ollama AI Chat",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Manages AI chat profiles for Ollama services.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]
