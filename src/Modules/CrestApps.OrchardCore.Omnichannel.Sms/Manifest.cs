using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SMS Omnichannel Automation",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "SMS Omnichannel Automation",
    Id = "CrestApps.OrchardCore.Omnichannel.Sms",
    Description = "Provides a way handle automated activities using the SMS channel.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.ChatCore,
        OmnichannelConstants.Features.Managements,
        "OrchardCore.Sms",
    ]
)]
