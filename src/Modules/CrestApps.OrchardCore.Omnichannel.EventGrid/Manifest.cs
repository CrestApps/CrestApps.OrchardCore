using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Omnichannel - Azure Event Grid",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Communication"
)]

[assembly: Feature(
    Name = "Omnichannel - Azure Event Grid",
    Id = "CrestApps.OrchardCore.Omnichannel.EventGrid",
    Category = "Communication",
    Description = "Receives authenticated Azure Event Grid events and dispatches them through the Omnichannel event pipeline.",
    Dependencies =
    [
        OmnichannelConstants.Features.Area,
    ]
)]
