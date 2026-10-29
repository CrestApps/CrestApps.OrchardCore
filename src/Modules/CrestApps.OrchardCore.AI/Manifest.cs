using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Prompting;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Artificial Intelligence",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AIConstants.Feature.Area,
    Name = "AI Services",
    Description = "Provides AI services.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ChatCore,
    Name = "AI Chat Services",
    Description = "Provides all the necessary services to enable chatting with AI models using profiles.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
        AIPromptingConstants.Feature.Area,
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ChatApi,
    Name = "AI Chat WebAPI",
    Description = "Provides a RESTful API for interacting with the AI chat.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.ChatCore,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ConnectionManagement,
    Name = "AI Connection Management",
    Description = "Provides user interface to manage AI connections.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ToolInstances,
    Name = "AI Tool Instances",
    Description = "Provides user interface to manage AI tool instances.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.Personas,
    Name = "AI Personas",
    Description = "Reusable characters for AI profiles and chat interactions, with their own instructions, assistant name, avatar and voice. Profiles link to a persona, so an edit applies to all of them from the next message.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.Quality,
    Name = "AI Quality",
    Description = "Simulation test cases you run against AI profiles from the admin or a CI pipeline, QA cohorts that score a sample of real conversations, a QA dashboard, and alert rules that watch the AI's metrics.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.CustomLlm,
    Name = "Custom LLM (WebSocket)",
    Description = "A provider that sends each turn to your own server over a WebSocket and streams its reply back, for a model or agent you host that does not speak an OpenAI-compatible API.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.Webhooks,
    Name = "AI Webhooks",
    Description = "Posts signed AI events (a conversation starting, ending, analyzed, transferred or scored, an alert triggering or resolving) to outside addresses, with retries and a delivery log.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]
