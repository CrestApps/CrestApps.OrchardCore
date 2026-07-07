using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Asterisk",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Integrates the Asterisk telephony platform with the Telephony soft phone.",
    Category = "Communication"
)]

[assembly: Feature(
    Id = AsteriskConstants.Feature.Area,
    Name = "Asterisk",
    Description = "Provides the Asterisk telephony provider and its settings.",
    Category = "Communication",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]
