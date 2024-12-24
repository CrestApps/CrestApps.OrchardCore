using OrchardCore.Modules.Manifest;

[assembly: Module(
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.OpenAI.Azure",
    Name = "Azure OpenAI Services",
    Description = "AI-powered chat using Azure OpenAI models.",
    Category = "OpenAI",
    EnabledByDependencyOnly = true
)]


[assembly: Feature(
    Id = "CrestApps.OrchardCore.OpenAI.Azure.Standard",
    Name = "Azure OpenAI",
    Description = "AI-powered chat using Azure OpenAI models.",
    Category = "OpenAI",
    Dependencies =
    [
        "CrestApps.OrchardCore.OpenAI",
        "CrestApps.OrchardCore.OpenAI.Azure",
    ]
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.OpenAI.Azure.AISearch",
    Name = "Azure OpenAI with Azure AI Search",
    Description = "AI-powered chat using Azure OpenAI models with data from Azure AI Search.",
    Category = "OpenAI",
    Dependencies =
    [
        "OrchardCore.Search.AzureAI",
        "CrestApps.OrchardCore.OpenAI",
        "CrestApps.OrchardCore.OpenAI.Azure",
    ]
)]
