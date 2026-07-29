using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.TimeZones;
using CrestApps.OrchardCore.Users.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Omnichannel Management",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Contact Center"
)]

[assembly: Feature(
    Name = "Omnichannel Activities",
    Id = OmnichannelConstants.Features.Activities,
    Category = "Contact Center",
    Description = "Adds the headless omnichannel contact, campaign, activity, disposition, subject-flow, and channel-endpoint services, permissions, and storage without any administration screens.",
    Dependencies =
    [
        OmnichannelConstants.Features.Area,
        UsersConstants.Feature.Area,
        "CrestApps.OrchardCore.ContentFields",
        PhoneNumberVerificationsConstants.Features.PhoneNumbers,
        "OrchardCore.Contents",
        "OrchardCore.Flows",
        "OrchardCore.Users",
        TimeZonesConstants.Features.Area,
        "CrestApps.OrchardCore.Users",
    ]
)]

[assembly: Feature(
    Name = "Omnichannel Management",
    Id = OmnichannelConstants.Features.Managements,
    Category = "Contact Center",
    Description = "Adds the omnichannel contact, campaign, activity, disposition, subject-flow, and channel-endpoint administration screens.",
    Dependencies =
    [
        OmnichannelConstants.Features.Activities,
        "CrestApps.OrchardCore.Resources",
        "OrchardCore.ContentTypes",
    ]
)]
