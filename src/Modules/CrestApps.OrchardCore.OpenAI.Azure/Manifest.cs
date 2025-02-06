using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Core;
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
        OpenAIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Deployments,
    Name = "Azure OpenAI Deployments",
    Description = "Manages AI deployments using Azure OpenAI models.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AzureOpenAIConstants.Feature.Area,
        AIConstants.Feature.Deployments,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Standard,
    Name = "Azure OpenAI Chat",
    Description = "Provides AI services using Azure OpenAI models.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        OpenAIConstants.Feature.ChatGPT,
        AzureOpenAIConstants.Feature.Deployments,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.AISearch,
    Name = "Azure OpenAI Chat with Your Data",
    Description = "AI chat using Azure OpenAI models with Azure AI Search data.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        "OrchardCore.Search.AzureAI",
        OpenAIConstants.Feature.ChatGPT,
        AzureOpenAIConstants.Feature.Deployments,
    ]
)]
