using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.ContentFields;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.Resources;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "CrestApps Content Fields",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Adds custom Orchard Core content fields maintained by CrestApps.",
    Category = "Content",
    IsAlwaysEnabled = false
)]

[assembly: Feature(
    Name = "CrestApps Content Fields",
    Id = "CrestApps.OrchardCore.ContentFields",
    Category = "Content",
    Description = "Adds custom Orchard Core content fields maintained by CrestApps.",
    Dependencies =
    [
        ResourcesConstants.Feature.Area,
        PhoneNumberVerificationsConstants.Features.PhoneNumbers,
        OrchardCoreFeatureIds.ContentFields,
        OrchardCoreFeatureIds.ContentTypes,
    ]
)]

[assembly: Feature(
    Name = "Conditional Fields",
    Id = ContentFieldsConstants.Features.ConditionalFields,
    Category = "Content",
    Description = "Shows a content field only when other answers say so, such as a model question only for laptops. The rules are checked again when the form is sent: what a hidden field holds is dropped.",
    Dependencies =
    [
        OrchardCoreFeatureIds.Contents,
        OrchardCoreFeatureIds.ContentTypes,
    ]
)]

[assembly: Feature(
    Name = "Address Field",
    Id = "CrestApps.OrchardCore.ContentFields.Address",
    Category = "Content",
    Description = "Adds an address field that lays itself out and checks the address the way the chosen country writes addresses.",
    Dependencies =
    [
        AddressConstants.Feature.ModuleId,
        OrchardCoreFeatureIds.ContentFields,
        OrchardCoreFeatureIds.ContentTypes,
    ]
)]
