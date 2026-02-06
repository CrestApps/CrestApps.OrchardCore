using CrestApps.OrchardCore;
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
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = McpConstants.Feature.Stdio,
    Name = "Model Context Protocol (MCP) Local Client",
    Description = "Extends the Model Context Protocol Client with standard input/output (STDIO) transport for connecting to local MCP servers.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
         McpConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = McpConstants.Feature.Server,
    Name = "Model Context Protocol (MCP) Server",
    Description = "Exposes Orchard Core AI tools through the MCP protocol, enabling external MCP-compatible clients to connect and invoke AI capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]
