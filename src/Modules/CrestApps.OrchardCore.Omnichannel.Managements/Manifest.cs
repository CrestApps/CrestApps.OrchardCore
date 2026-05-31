using CrestApps.OrchardCore;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Users.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Omnichannel Management",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Communications"
)]

[assembly: Feature(
    Name = "Omnichannel Management",
    Id = OmnichannelConstants.Features.Managements,
    Category = "Communications",
    Description = "Provides way to manage Omnichannel Contacts",
    Dependencies =
    [
        OmnichannelConstants.Features.Area,
        UsersConstants.Feature.Area,
        "CrestApps.OrchardCore.Resources",
        "OrchardCore.ContentTypes",
        "OrchardCore.Flows",
        "OrchardCore.Users",
        "CrestApps.OrchardCore.Users",
    ]
)]

[assembly: Feature(
    Name = "Omnichannel National Do Not Call Registry",
    Id = OmnichannelConstants.Features.NationalDoNotCallRegistry,
    Category = "Communications",
    Description = "Checks phone numbers against national do-not-call registries during contact import.",
    Dependencies =
    [
        OmnichannelConstants.Features.Managements,
        DncRegistryConstants.Features.Area,
    ]
)]
