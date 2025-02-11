using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Area,
    Name = "Azure OpenAI Services",
    Description = "Provides AI services using Azure OpenAI models.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        AIConstants.Feature.Deployments,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Standard,
    Name = "Azure OpenAI Chat",
    Description = "Enables integration with OpenAI through the Azure service provider.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Chat,
        AzureOpenAIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.AISearch,
    Name = "Azure OpenAI Chat with Your Data",
    Description = "Enables integration with OpenAI and Azure AI Search data via the Azure service provider.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Chat,
        AzureOpenAIConstants.Feature.Area,
        "OrchardCore.Search.AzureAI",
    ]
)]
