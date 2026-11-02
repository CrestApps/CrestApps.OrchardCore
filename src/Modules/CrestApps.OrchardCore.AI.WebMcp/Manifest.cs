using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.WebMcp;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "WebMCP",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = WebMcpConstants.Feature.Area,
    Name = "WebMCP",
    Description = "Gives AI agents in the browser, and the AI chat, tools on the pages a signed-in user opens through the standard WebMCP API: the AI tools you select for the site or for a content item, and page tools that read the page and fill its forms.",
    Category = "Artificial Intelligence - MCP",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]
