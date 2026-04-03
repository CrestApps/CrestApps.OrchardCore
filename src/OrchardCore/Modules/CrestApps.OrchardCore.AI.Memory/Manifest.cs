using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Memory;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Memory",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
    )]

[assembly: Feature(
    Id = MemoryConstants.Feature.Memory,
    Name = "AI Memory",
    Description = "Provides persistent, user-scoped AI memory for AI profiles and chat interactions.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
    AIConstants.Feature.ChatCore,
    "OrchardCore.Indexing",
    ]
    )]
