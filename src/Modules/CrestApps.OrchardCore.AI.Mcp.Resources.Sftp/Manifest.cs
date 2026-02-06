using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Mcp.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Model Context Protocol (MCP) SFTP Resource",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.AI.Mcp.Resources.Sftp",
    Name = "Model Context Protocol (MCP) SFTP Resource",
    Description = "Provides SFTP resource support for the MCP Server, allowing remote files to be exposed as MCP resources using sftp:// URIs.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        McpConstants.Feature.Server,
    ]
)]
