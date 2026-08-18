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
    Description = "Provides a reusable, multi-step wizard (stepper) framework usable via code.",
    Category = "Content Management"
)]

[assembly: Feature(
    Name = "Wizard Contents",
    Id = WizardConstants.Features.Contents,
    Description = "Lets editors build wizards from content items using the wizard part.",
    Category = "Content Management",
    Dependencies =
    [
        WizardConstants.Features.Area,
        "OrchardCore.Contents",
        "OrchardCore.Flows",
    ]
)]

[assembly: Feature(
    Name = "Wizard Workflows",
    Id = WizardConstants.Features.Workflows,
    Description = "Raises workflow events for wizard and step lifecycle transitions.",
    Category = "Content Management",
    Dependencies =
    [
        WizardConstants.Features.Area,
        "OrchardCore.Workflows",
    ]
)]
