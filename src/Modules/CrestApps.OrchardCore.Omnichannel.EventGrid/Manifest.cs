using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Omnichannel (Azure Event Grid)",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Communications"
)]

[assembly: Feature(
    Name = "Omnichannel (Azure Event Grid)",
    Id = "CrestApps.OrchardCore.Omnichannel.EventGrid",
    Category = "Communications",
    Description = "Provides was to communicate using Azure Event Grid",
    Dependencies =
    [
        OmnichannelConstants.Features.Area,
    ]
)]
