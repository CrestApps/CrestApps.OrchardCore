using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "OpenAI Chat",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = OpenAIConstants.Feature.Area,
    Name = "OpenAI Chat",
    Description = "Provides a way to interact with the OpenAI service provider.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.Deployments,
    ]
)]

[assembly: Feature(
    Id = OpenAIConstants.Feature.Settings,
    Name = "OpenAI-Compatible Chat",
    Description = "Provides a way to interact with any provider that is compatible with OpenAI.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.Deployments,
    ]
)]
