using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.Resources;
using CrestApps.OrchardCore.TimeZones;
using CrestApps.OrchardCore.Users;
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
        UsersConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Name = "Omnichannel Channel Endpoints",
    Id = OmnichannelConstants.Features.ChannelEndpoints,
    Category = "Contact Center",
    Description = "Adds only the channel-endpoint (number/address) administration screen and services, so a feature that needs to reuse channel endpoints can depend on this without pulling in the full Omnichannel management screens.",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        OmnichannelConstants.Features.Activities,
        ResourcesConstants.Feature.Area,
        "OrchardCore.Resources",
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
        OmnichannelConstants.Features.ChannelEndpoints,
        ResourcesConstants.Feature.Area,
        "OrchardCore.Resources",
        "OrchardCore.ContentTypes",
    ]
)]

[assembly: Feature(
    Name = "Omnichannel CRM",
    Id = OmnichannelConstants.Features.Crm,
    Category = "Contact Center",
    Description = "Adds leads, accounts and opportunities to the omnichannel CRM: lead records that are called and texted like contacts but kept apart until they are converted, accounts that hold contacts and opportunities, and opportunity stages for the pipeline.",
    Dependencies =
    [
        OmnichannelConstants.Features.Managements,

        // Accounts are the Accounts feature's content type; the CRM puts contacts and opportunities in them.
        AccountsConstants.Feature,
        "OrchardCore.ContentFields",
        "OrchardCore.Lists",
        "OrchardCore.Title",
    ]
)]
