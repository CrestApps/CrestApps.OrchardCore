using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Azure OpenAI Services",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.OpenAI.Azure",
    Name = "Azure OpenAI Chat",
    Description = "Provides AI services using Azure OpenAI models.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]
