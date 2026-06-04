using CrestApps.OrchardCore;
using CrestApps.OrchardCore.DncRegistry;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "DNC Registry",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Compliance"
)]

[assembly: Feature(
    Name = "DNC Registry",
    Id = DncRegistryConstants.Features.Area,
    Category = "Compliance",
    Description = "Provides the core framework for integrating with national do-not-call registries."
)]

[assembly: Feature(
    Name = "USA FTC Do Not Call Registry",
    Id = DncRegistryConstants.Features.UsaFtc,
    Category = "Compliance",
    Description = "Integrates with the United States Federal Trade Commission (FTC) National Do Not Call Registry at telemarketing.donotcall.gov.",
    Dependencies =
    [
        DncRegistryConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Name = "Canada LNNTE-DNCL Registry",
    Id = DncRegistryConstants.Features.CanadaDncl,
    Category = "Compliance",
    Description = "Integrates with the Canadian National Do Not Call List (LNNTE-DNCL) maintained by the CRTC.",
    Dependencies =
    [
        DncRegistryConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Name = "Local Do Not Call Registry",
    Id = DncRegistryConstants.Features.Local,
    Category = "Compliance",
    Description = "Provides a local do-not-call registry where administrators can upload CSV files of phone numbers organized by country.",
    Dependencies =
    [
        DncRegistryConstants.Features.Area,
    ]
)]
