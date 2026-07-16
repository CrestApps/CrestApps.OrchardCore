using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Telephony",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides a provider-agnostic soft phone and SignalR hub for integrating telephony providers.",
    Category = "Telephony"
)]

[assembly: Feature(
    Id = TelephonyConstants.Feature.Area,
    Name = "Telephony",
    Description = "Provides the provider-agnostic telephony services, SignalR hub, and site settings.",
    Category = "Telephony",
    Dependencies =
    [
        "OrchardCore.Users",
        "CrestApps.OrchardCore.SignalR",
    ]
)]

[assembly: Feature(
    Id = TelephonyConstants.Feature.SoftPhone,
    Name = "Telephony Soft Phone",
    Description = "Injects the floating soft phone experience into the admin dashboard, front end, or both.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]
