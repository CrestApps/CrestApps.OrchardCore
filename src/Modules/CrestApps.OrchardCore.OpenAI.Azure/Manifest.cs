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
    Name = "Azure OpenAI-Powered Artificial Intelligence",
    Description = "AI-powered chat using Azure OpenAI models.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        OpenAIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Deployments,
    Name = "AzureOpenAI-Powered Artificial Intelligence Deployments",
    Description = "AI deployments utilizing models available through Azure OpenAI.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AzureOpenAIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Standard,
    Name = "Azure OpenAI-Powered Artificial Intelligence Chat",
    Description = "AI-powered chat using Azure OpenAI models.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        OpenAIConstants.Feature.ChatGPT,
        AzureOpenAIConstants.Feature.Deployments,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.AISearch,
    Name = "Azure OpenAI-Powered Artificial Intelligence Chat with Azure AI Search",
    Description = "AI-powered chat using Azure OpenAI models with data from Azure AI Search.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        "OrchardCore.Search.AzureAI",
        OpenAIConstants.Feature.ChatGPT,
    ]
)]
