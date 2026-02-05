using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Mcp.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "MCP File Resource",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.AI.Mcp.Resources.File",
    Name = "MCP File Resource",
    Description = "Provides file-based resource support for the MCP Server, allowing local files to be exposed as MCP resources using file:// URIs.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        McpConstants.Feature.Server,
    ]
)]
