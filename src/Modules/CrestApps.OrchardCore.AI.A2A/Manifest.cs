using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.A2A;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Agent-to-Agent (A2A) Protocol",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
    )]

[assembly: Feature(
    Id = A2AConstants.Feature.Area,
    Name = "Agent-to-Agent (A2A) Client",
    Description = "Provides a user interface for connecting to remote Agent-to-Agent (A2A) hosts, enabling AI profiles to leverage external agents for multi-agent orchestration.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
    AIConstants.Feature.Area,
    "CrestApps.OrchardCore.Resources",
    ]
    )]

[assembly: Feature(
    Id = A2AConstants.Feature.Host,
    Name = "Agent-to-Agent (A2A) Host",
    Description = "Exposes all AI Agent profiles through the A2A protocol, enabling external agents and clients to discover and communicate with locally hosted agents.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
    AIConstants.Feature.Area,
    ]
    )]
