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
    Id = TelephonyConstants.Feature.SoftPhoneCore,
    Name = "Telephony Soft Phone Core",
    Description = "Provides the shared soft phone client, presenter, and resources reused by the soft phone widget and the browser-extension endpoint.",
    Category = "Telephony",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = TelephonyConstants.Feature.SoftPhone,
    Name = "Telephony Soft Phone",
    Description = "Adds the floating soft phone to the admin dashboard, and provides a Soft Phone widget to place it on the front end.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.SoftPhoneCore,
        "OrchardCore.Widgets",
    ]
)]

[assembly: Feature(
    Id = TelephonyConstants.Feature.SoftPhoneExtension,
    Name = "Telephony Soft Phone Extension",
    Description = "Exposes the standalone /softphone page and configuration endpoint hosted by the CrestApps Soft Phone browser extension so calls survive navigation and ring when the phone is closed.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.SoftPhoneCore,
    ]
)]
