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
    Name = "WebMCP Tools",
    Description = "Gives selected AI tools to AI agents in the browser through WebMCP, on the pages a signed-in user opens.",
    Category = "Artificial Intelligence - MCP",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = WebMcpConstants.Feature.PageTools,
    Name = "WebMCP Page Tools",
    Description = "Adds WebMCP tools that work on the page the user sees: page info, links, opening a page, and reading and filling forms.",
    Category = "Artificial Intelligence - MCP",
    Dependencies =
    [
        WebMcpConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = WebMcpConstants.Feature.Chat,
    Name = "WebMCP Chat Bridge",
    Description = "Lets the AI chat call the WebMCP tools on the page the user has open, for the AI profiles that allow it.",
    Category = "Artificial Intelligence - MCP",
    Dependencies =
    [
        WebMcpConstants.Feature.Area,
        AIConstants.Feature.Chat,
    ]
)]
