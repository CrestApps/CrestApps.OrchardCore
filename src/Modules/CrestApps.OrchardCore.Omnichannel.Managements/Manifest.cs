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
    Category = "Communication"
)]

[assembly: Feature(
    Name = "Omnichannel Management",
    Id = OmnichannelConstants.Features.Managements,
    Category = "Communication",
    Description = "Adds omnichannel contact, campaign, activity, disposition, subject-flow, and channel-endpoint management.",
    Dependencies =
    [
        OmnichannelConstants.Features.Area,
        UsersConstants.Feature.Area,
        "CrestApps.OrchardCore.ContentFields",
        PhoneNumberVerificationsConstants.Features.PhoneNumbers,
        "CrestApps.OrchardCore.Resources",
        "OrchardCore.ContentTypes",
        "OrchardCore.Contents",
        "OrchardCore.Flows",
        "OrchardCore.Users",
        TimeZonesConstants.Features.Area,
        "CrestApps.OrchardCore.Users",
    ]
)]
