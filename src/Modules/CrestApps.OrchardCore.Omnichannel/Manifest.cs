using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Omnichannel",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Contact Center"
)]

[assembly: Feature(
    Name = "Omnichannel",
    Id = OmnichannelConstants.Features.Area,
    Category = "Contact Center",
    Description = "Provides shared omnichannel messages, endpoints, preferences, and processing contracts across communication channels."
)]

[assembly: Feature(
    Name = "Omnichannel - Azure Communication Services",
    Id = OmnichannelConstants.Features.AzureCommunicationServices,
    Category = "Contact Center",
    Description = "Enables Azure Communication Services email and SMS providers for omnichannel communications.",
    Dependencies =
    [
        OmnichannelConstants.Features.Area,
        "OrchardCore.Email.Azure",
        "OrchardCore.Sms.Azure",
    ]
)]
