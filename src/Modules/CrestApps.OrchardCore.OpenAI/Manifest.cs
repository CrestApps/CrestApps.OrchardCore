using CrestApps.OrchardCore.OpenAI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "OpenAI",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = OpenAIConstants.Feature.Area,
    Name = "OpenAI",
    Description = "Provides OpenAI services.",
    Category = "OpenAI",
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = OpenAIConstants.Feature.ChatGPT,
    Name = "OpenAI Chat",
    Description = "Provides a way to manage AI chat profiles for OpenAI models.",
    Category = "OpenAI",
    Dependencies =
    [
        "CrestApps.OrchardCore.Resources",
        OpenAIConstants.Feature.Area,
    ]
)]
