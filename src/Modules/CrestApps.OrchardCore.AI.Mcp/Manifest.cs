using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Model Context Protocol (MCP)",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = McpConstants.Feature.Area,
    Name = "Model Context Protocol (MCP)",
    Description = "Provides a way to connect AI models with Model Context Protocol (MCP) Servers.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
         AIConstants.Feature.Area,
    ]
)]
