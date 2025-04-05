using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Model Context Protocol (MCP) Clients",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.AI.Mcp",
    Name = "Model Context Protocol (MCP) Clients",
    Description = "Provides a way to connect AI models with Model Context Protocol (MCP) Servers.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
         AIConstants.Feature.Area,
    ]
)]
