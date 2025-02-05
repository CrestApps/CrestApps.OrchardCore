using CrestApps.OrchardCore.AI.Core;
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
    Name = "OpenAI Services",
    Description = "Provides core AI services for OpenAI technology.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        AIConstants.Feature.Area,
        "OrchardCore.Markdown",
    ]
)]

[assembly: Feature(
    Id = OpenAIConstants.Feature.ChatGPT,
    Name = "OpenAI Services Chat",
    Description = "Manages AI chat profiles for OpenAI models.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        AIConstants.Feature.Chat,
        OpenAIConstants.Feature.Area,
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
    ]
)]
