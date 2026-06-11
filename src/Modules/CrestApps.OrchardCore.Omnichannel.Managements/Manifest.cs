using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.TimeZones;
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
        "CrestApps.OrchardCore.ContentFields",
        PhoneNumbersConstants.Features.Area,
        "CrestApps.OrchardCore.Resources",
        "OrchardCore.ContentTypes",
        "OrchardCore.Flows",
        "OrchardCore.Users",
        TimeZonesConstants.Features.Area,
        "CrestApps.OrchardCore.Users",
    ]
)]
