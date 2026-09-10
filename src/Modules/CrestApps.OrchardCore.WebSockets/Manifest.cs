using CrestApps.OrchardCore;
using CrestApps.OrchardCore.WebSockets;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "WebSockets",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Enables ASP.NET Core WebSocket hosting for the tenant and provides a home for reusable WebSocket infrastructure.",
    Category = "Infrastructure"
)]

[assembly: Feature(
    Id = WebSocketsConstants.Feature.Area,
    Name = "WebSockets",
    Description = "Adds the ASP.NET Core WebSocket middleware to the tenant pipeline so features can host raw WebSocket endpoints. Enabled automatically as a dependency of features that need it.",
    Category = "Infrastructure",
    EnabledByDependencyOnly = true
)]
