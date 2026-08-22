using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Wizard;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Wizard",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Wizard",
    Id = WizardConstants.Features.Area,
    Description = "Provides a reusable, multi-step wizard (stepper) services.",
    Category = "Content Management"
)]
