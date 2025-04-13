using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Model Context Protocol (MCP) Client",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = McpConstants.Feature.Area,
    Name = "Model Context Protocol (MCP) Client",
    Description = "Offers core services and a user interface for connecting to Model Context Protocol (MCP) servers, enabling AI models to leverage additional capabilities and resources.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = McpConstants.Feature.Stdio,
    Name = "Model Context Protocol (Local MCP) Client",
    Description = "Extends the Model Context Protocol Client with standard input/output (STDIO) transport for connecting to local MCP servers.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
         McpConstants.Feature.Area,
    ]
)]
