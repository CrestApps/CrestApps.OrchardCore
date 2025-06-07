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
        AIConstants.Feature.Area,
        AzureOpenAIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.DataSources,
    Name = "Azure OpenAI – Bring Your Own Data",
    Description = "Provides you a way to connect your Azure OpenAI with defined data sources.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.DataSources,
        AzureOpenAIConstants.Feature.Area,
    ],
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.AISearch,
    Name = "Azure AI Search-Powered Data Source",
    Description = "Enables integration with OpenAI and Azure AI Search data via the Azure service provider.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AzureOpenAIConstants.Feature.DataSources,
        "OrchardCore.Search.AzureAI",
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.Elasticsearch,
    Name = "Elasticsearch-Powered Data Source",
    Description = "Enables integration with OpenAI and Elasticsearch data via the Azure service provider.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AzureOpenAIConstants.Feature.DataSources,
        "OrchardCore.Search.Elasticsearch",
    ]
)]

[assembly: Feature(
    Id = AzureOpenAIConstants.Feature.MongoDB,
    Name = "MongoDB-Powered Data Source",
    Description = "Enables integration with OpenAI and MongoDB data via the Azure service provider.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AzureOpenAIConstants.Feature.DataSources,
    ]
)]
