using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "CrestApps Resources",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Extends the Resources module with additional reusable scripts and stylesheets.",
    Category = "Theming",
    Dependencies =
    [
        "OrchardCore.Resources",
    ],
    IsAlwaysEnabled = true
)]
