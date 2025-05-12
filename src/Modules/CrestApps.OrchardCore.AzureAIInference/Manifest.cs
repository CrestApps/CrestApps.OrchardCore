using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Azure AI Inference Chat",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.AzureAIInference",
    Name = "Azure AI Inference Chat",
    Description = "Provides a way to interact with GitHub Models using Azure Inference service provider.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.Deployments,
    ]
)]
