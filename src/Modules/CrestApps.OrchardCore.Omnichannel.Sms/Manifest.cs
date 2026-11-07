using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Webhooks;
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
    Description = "Handles automated omnichannel activities through the configured Orchard Core SMS provider.",
    Category = "Contact Center",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.ChatCore,
        OmnichannelConstants.Features.Managements,
        OrchardCoreFeatureIds.Sms,

        // Every SMS webhook commits an inbound text to the durable provider webhook inbox before it is answered, so the
        // text survives a restart and a redelivery is absorbed on any node.
        WebhooksConstants.Feature.ProviderInbox,
    ]
)]
