using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.DeepSeek.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "OpenAI",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = DeepSeekConstants.Feature.Area,
    Name = "Artificial Intelligence Powered by DeepSeek",
    Description = "Provides essential services for any DeepSeek technology provider.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        AIConstants.Feature.Area,
        "OrchardCore.Markdown",
    ]
)]

[assembly: Feature(
    Id = DeepSeekConstants.Feature.Chat,
    Name = "Artificial Intelligence Powered by DeepSeek Chat",
    Description = "Provides a way to manage AI chat profiles for any DeepSeek provider.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        AIConstants.Feature.Chat,
        DeepSeekConstants.Feature.Area,
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = DeepSeekConstants.Feature.CloudChat,
    Name = "Artificial Intelligence Powered by DeepSeek Cloud Service Chat",
    Description = "Provides a way to manage AI chat profiles for DeepSeek cloud service.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        DeepSeekConstants.Feature.Chat,
    ]
)]
