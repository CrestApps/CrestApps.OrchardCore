using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Id = AIConstants.Feature.OrchardCoreAIAgent,
    Name = "Orchard Core AI Agent",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Use natural language to run tasks, manage content, interact with OrchardCore features, and do much more with integrated AI-powered tools.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgent,
    Name = "Orchard Core AI Agent",
    Description = "Use natural language to run tasks, manage content, interact with OrchardCore features, and do much more with integrated AI-powered tools.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentRecipes,
    Name = "Orchard Core AI Agent - Recipe Capabilities",
    Description = "Adds possibility to enable recipes related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentTenants,
    Name = "Orchard Core AI Agent - Tenant Capabilities",
    Description = "Adds possibility to enable tenant related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentContents,
    Name = "Orchard Core AI Agent - Content Capabilities",
    Description = "Adds possibility to enable content related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentContentDefinitionRecipesTools,
    Name = "Orchard Core AI Agent - Content Definition Recipes Capabilities",
    Description = "Adds possibility to enable content definition recipes related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentContentDefinitions,
    Name = "Orchard Core AI Agent - Content Definitions Capabilities",
    Description = "Adds possibility to enable content definitions related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentFeatures,
    Name = "Orchard Core AI Agent - Features Capabilities",
    Description = "Adds possibility to enable features related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentNotifications,
    Name = "Orchard Core AI Agent - Notification Capabilities",
    Description = "Adds possibility to enable notification related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentEmail,
    Name = "Orchard Core AI Agent - Email Capabilities",
    Description = "Adds possibility to enable email related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentSms,
    Name = "Orchard Core AI Agent - Sms Capabilities",
    Description = "Adds possibility to enable sms related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentUsers,
    Name = "Orchard Core AI Agent - Users Capabilities",
    Description = "Adds possibility to enable users related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentRoles,
    Name = "Orchard Core AI Agent - Roles Capabilities",
    Description = "Adds possibility to enable roles related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentWorkflows,
    Name = "Orchard Core AI Agent - Workflows Capabilities",
    Description = "Adds possibility to enable workflows related capabilities.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]
