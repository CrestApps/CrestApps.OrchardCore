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
    Description = "AI-powered chat using Azure OpenAI models.",
    Category = "OpenAI",
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Deployments,
    Name = "Azure OpenAI Deployments",
    Description = "AI deployments utilizing models available through Azure OpenAI.",
    Category = "OpenAI",
    Dependencies =
    [
        OpenAIConstants.Feature.Area,
        AzureOpenAIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Standard,
    Name = "Azure OpenAI",
    Description = "AI-powered chat using Azure OpenAI models.",
    Category = "OpenAI",
    Dependencies =
    [
        OpenAIConstants.Feature.ChatGPT,
        AzureOpenAIConstants.Feature.Area,
        AzureOpenAIConstants.Feature.Deployments,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.AISearch,
    Name = "Azure OpenAI with Azure AI Search",
    Description = "AI-powered chat using Azure OpenAI models with data from Azure AI Search.",
    Category = "OpenAI",
    Dependencies =
    [
        "OrchardCore.Search.AzureAI",
        OpenAIConstants.Feature.ChatGPT,
        AzureOpenAIConstants.Feature.Area,
        AzureOpenAIConstants.Feature.Deployments,
    ]
)]
