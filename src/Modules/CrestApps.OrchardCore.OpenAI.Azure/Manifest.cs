using OrchardCore.Modules.Manifest;

[assembly: Module(
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.OpenAI.Azure.Core",
    Name = "Azure OpenAI Services",
    Description = "AI-powered chat using Azure OpenAI models",
    Category = "OpenAI",
    EnabledByDependencyOnly = true
)]


[assembly: Feature(
    Id = "CrestApps.OrchardCore.OpenAI.Azure",
    Name = "Azure OpenAI",
    Description = "AI-powered chat using Azure OpenAI models",
    Category = "OpenAI",
    Dependencies =
    [
        "CrestApps.OrchardCore.OpenAI.Azure.Core",
        "CrestApps.OrchardCore.OpenAI",
    ]
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.OpenAI.Azure.SearchAI",
    Name = "Azure OpenAI with Azure Search AI",
    Description = "AI-powered chat using Azure OpenAI models with data from Azure Search AI.",
    Category = "OpenAI",
    Dependencies =
    [
        "CrestApps.OrchardCore.OpenAI.Azure.Core",
        "CrestApps.OrchardCore.OpenAI",
        "OrchardCore.Search.AzureAI",
    ]
)]
