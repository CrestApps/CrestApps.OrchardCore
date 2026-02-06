using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Mcp.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Model Context Protocol (MCP) FTP Resource",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.AI.Mcp.Resources.Ftp",
    Name = "Model Context Protocol (MCP) FTP Resource",
    Description = "Provides FTP/FTPS resource support for the MCP Server, allowing remote files to be exposed as MCP resources.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        McpConstants.Feature.Server,
    ]
)]
