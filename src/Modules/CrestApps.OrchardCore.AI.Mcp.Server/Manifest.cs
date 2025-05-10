using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Model Context Protocol (MCP) Server",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides a way to enable Context Protocol (MCP) Servers on your site.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
         AIConstants.Feature.Area,
    ]
)]
