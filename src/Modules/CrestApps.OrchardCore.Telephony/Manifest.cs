using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Resources;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Users;
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
        OrchardCoreFeatureIds.Users,
        OrchardCoreFeatureIds.SignalR,

        // The extension editor picks its user with the searchable user picker (CrestApps Users), which renders through
        // the item selector (CrestApps Resources), rather than loading every user into a list.
        UsersConstants.Feature.Area,
        ResourcesConstants.Feature.Area,
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
        ResourcesConstants.Feature.Area,
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
        OrchardCoreFeatureIds.Widgets,
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
