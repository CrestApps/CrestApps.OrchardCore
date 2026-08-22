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
        "OrchardCore.SignalR",
    ]
)]

[assembly: Feature(
    Id = TelephonyConstants.Feature.SoftPhone,
    Name = "Telephony Soft Phone",
    Description = "Adds the floating soft phone to the admin dashboard, and provides a Soft Phone widget to place it on the front end.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
        "CrestApps.OrchardCore.Resources",
        "OrchardCore.Widgets",
    ]
)]
