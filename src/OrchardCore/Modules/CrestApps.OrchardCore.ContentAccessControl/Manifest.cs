using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Content Access Control",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Enables you to control access to your content by assigning specific roles, ensuring only authorized users can view or manage items.",
    Category = "Content Management",
    Dependencies =
    [
        "CrestApps.OrchardCore.Roles",
    ]
)]
