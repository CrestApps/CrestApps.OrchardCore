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
    Description = "Handles automated omnichannel activities through the configured Orchard Core SMS provider.",
    Category = "Contact Center",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.ChatCore,
        OmnichannelConstants.Features.Managements,
        "OrchardCore.Sms",

        // Automated SMS conversations use business-hours calendars to keep background-initiated sends (re-engagement
        // nudges) within hours, so enabling this feature enables the Business Hours feature that registers the gate and
        // the calendar administration. Referenced by feature id (resolved at runtime) to avoid an assembly reference.
        "CrestApps.OrchardCore.ContactCenter.BusinessHours",
    ]
)]
