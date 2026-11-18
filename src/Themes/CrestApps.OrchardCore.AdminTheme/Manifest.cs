using CrestApps.OrchardCore;
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "CrestApps Admin Theme",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "A modern admin theme built on The Admin Theme: a full height branded menu that still collapses to icons and opens as a drawer on small screens, a lighter top bar, and softer cards, lists, forms and tabs. It stays on Bootstrap 5.3, so every admin page carries over.",
    BaseTheme = "TheAdmin",
    Tags = ["Admin"]
)]
