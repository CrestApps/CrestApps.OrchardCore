using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "CrestApps Recipes",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Enhances Orchard Core recipe functionality by enabling detailed descriptions for recipes.",
    Category = "Infrastructure",
    EnabledByDependencyOnly = true,
    Dependencies = new[]
    {
        "OrchardCore.Recipes.Core",
    }
)]
