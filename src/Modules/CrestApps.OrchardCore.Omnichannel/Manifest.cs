using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Omnichannel",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Communications"
)]

[assembly: Feature(
    Name = "Omnichannel",
    Id = OmnichannelConstants.Features.Area,
    Category = "Communications",
    Description = "Provides a unified communication layer that works across any channel (SMS, email, chat, phone, and more)"
)]

[assembly: Feature(
    Name = "Omnichannel Management",
    Id = OmnichannelConstants.Features.Managements,
    Category = "Communications",
    Description = "Provides was to manage Omnichannel Contacts",
    Dependencies =
    [
        OmnichannelConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Name = "Omnichannel (Azure Communication Services)",
    Id = OmnichannelConstants.Features.AzureCommunicationServices,
    Category = "Communications",
    Description = "Provides was to communicate using Azure Communication Services",
    Dependencies =
    [
        OmnichannelConstants.Features.Area,
    ]
)]
