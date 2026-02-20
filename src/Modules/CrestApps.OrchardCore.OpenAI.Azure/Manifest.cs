using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Azure OpenAI Services",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Area,
    Name = "Azure OpenAI Chat",
    Description = "Provides AI services using Azure OpenAI models.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Deployments,
    ]
)]
