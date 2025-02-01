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
    Name = "Artificial Intelligence Powered by OpenAI",
    Description = "Provides essential services for any OpenAI technology provider.",
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
    Name = "Artificial Intelligence Powered by OpenAI Chat",
    Description = "Provides a way to manage AI chat profiles for any OpenAI provider.",
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
