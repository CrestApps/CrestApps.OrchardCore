using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Analytics;
using CrestApps.OrchardCore.AI.Agent.Communications;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.Features;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Agent.Roles;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Agent.System;
using CrestApps.OrchardCore.AI.Agent.Tenants;
using CrestApps.OrchardCore.AI.Agent.Users;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
[Feature(AIConstants.Feature.OrchardCoreAIAgent)]
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListTimeZoneTool>(ListTimeZoneTool.TheName)
            .WithTitle(S["List System Time Zones"])
            .WithDescription(S["Retrieves a list of the available time zones in the system."])
            .WithCategory(S["System"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Recipes feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipesStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RecipesStartup(IStringLocalizer<RecipesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ApplySystemSettingsTool>(ApplySystemSettingsTool.TheName)
            .WithTitle(S["Apply Site Configuration"])
            .WithDescription(S["Applies predefined system configurations and settings using AI assistance."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddCoreAITool<GetRecipeJsonSchemaTool>(GetRecipeJsonSchemaTool.TheName)
            .WithTitle(S["Get Orchard Core Recipe JSON Schema"])
            .WithDescription(S["Returns a JSON Schema definition for Orchard Core recipes or a specific recipe step."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddCoreAITool<ListRecipeStepsAndSchemasTool>(ListRecipeStepsAndSchemasTool.TheName)
            .WithTitle(S["List Orchard Core Recipe Steps and Schemas"])
            .WithDescription(S["Lists all available Orchard Core recipe steps and returns their JSON schema definitions."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddCoreAITool<ImportOrchardTool>(ImportOrchardTool.TheName)
            .WithTitle(S["Import Orchard Core Recipe"])
            .WithDescription(S["Enables AI agents to import and run Orchard Core recipes within your site."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddCoreAITool<ListNonStartupRecipesTool>(ListNonStartupRecipesTool.TheName)
            .WithTitle(S["List Non-Startup Recipes"])
            .WithDescription(S["Retrieves all available Orchard Core recipes that are not executed during startup."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddCoreAITool<ExecuteStartupRecipesTool>(ExecuteStartupRecipesTool.TheName)
            .WithTitle(S["Run Non-Startup Recipes"])
            .WithDescription(S["Executes Orchard Core recipes that are not configured to run at application startup."])
            .WithCategory(S["Recipes"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Tenants feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Tenants")]
public sealed class TenantsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TenantsStartup(IStringLocalizer<TenantsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListStartupRecipesTool>(ListStartupRecipesTool.TheName)
            .WithTitle(S["List Startup Recipes"])
            .WithDescription(S["Retrieves a list of Orchard Core recipes configured to run at application startup."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddCoreAITool<CreateTenantTool>(CreateTenantTool.TheName)
            .WithTitle(S["Create Tenant"])
            .WithDescription(S["Creates a new tenant in the Orchard Core application."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddCoreAITool<GetTenantTool>(GetTenantTool.TheName)
            .WithTitle(S["Get Tenant Information"])
            .WithDescription(S["Retrieves detailed information about a specific tenant."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddCoreAITool<ListTenantTool>(ListTenantTool.TheName)
            .WithTitle(S["List All Tenants"])
            .WithDescription(S["Returns information about all tenants in the system."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddCoreAITool<EnableTenantTool>(EnableTenantTool.TheName)
            .WithTitle(S["Enable Tenant"])
            .WithDescription(S["Enables a tenant that is currently disabled."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddCoreAITool<DisableTenantTool>(DisableTenantTool.TheName)
            .WithTitle(S["Disable Tenant"])
            .WithDescription(S["Disables a tenant that is currently active."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddCoreAITool<RemoveTenantTool>(RemoveTenantTool.TheName)
            .WithTitle(S["Remove Tenant"])
            .WithDescription(S["Removes an existing tenant that can be safely deleted."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddCoreAITool<ReloadTenantTool>(ReloadTenantTool.TheName)
            .WithTitle(S["Reload Tenant"])
            .WithDescription(S["Reloads the configuration and state of an existing tenant."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddCoreAITool<SetupTenantTool>(SetupTenantTool.TheName)
            .WithTitle(S["Setup Tenant"])
            .WithDescription(S["Sets up new tenants."])
            .WithCategory(S["Tenants Management"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Contents feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Contents")]
public sealed class ContentsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentsStartup(IStringLocalizer<ContentsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SearchForContentsTool>(SearchForContentsTool.TheName)
            .WithTitle(S["Search Content Items"])
            .WithDescription(S["Provides a way to search for content items."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<GetContentItemSchemaTool>(GetContentItemSchemaTool.TheName)
            .WithTitle(S["Generate Content Item Sample"])
            .WithDescription(S["Generates a structured sample content item for a specified content type."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<PublishContentTool>(PublishContentTool.TheName)
            .WithTitle(S["Publish Content Item"])
            .WithDescription(S["Publishes a draft or previously unpublished content item."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<UnpublishContentTool>(UnpublishContentTool.TheName)
            .WithTitle(S["Unpublish Content Item"])
            .WithDescription(S["Unpublishes a currently published content item."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<GetContentTool>(GetContentTool.TheName)
            .WithTitle(S["Retrieve Content Item"])
            .WithDescription(S["Retrieves a specific content item by its ID or type."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<DeleteContentTool>(DeleteContentTool.TheName)
            .WithTitle(S["Delete Content Item"])
            .WithDescription(S["Deletes a content item from the system."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<CloneContentTool>(CloneContentTool.TheName)
            .WithTitle(S["Clone Content Item"])
            .WithDescription(S["Creates a duplicate of an existing content item."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<CreateOrUpdateContentTool>(CreateOrUpdateContentTool.TheName)
            .WithTitle(S["Create or Update Content Item"])
            .WithDescription(S["Creates a new content item or updates an existing one."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<GetContentItemLinkTool>(GetContentItemLinkTool.TheName)
            .WithTitle(S["Retrieve a Link for a Content Item"])
            .WithDescription(S["Retrieves a link for a content item."])
            .WithCategory(S["Content Management"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the ContentDefinitions feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.ContentTypes")]
public sealed class ContentDefinitionsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDefinitionsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentDefinitionsStartup(IStringLocalizer<ContentDefinitionsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ContentMetadataService>();

        services.AddCoreAITool<GetContentTypeDefinitionsTool>(GetContentTypeDefinitionsTool.TheName)
            .WithTitle(S["Get Content Type Definitions"])
            .WithDescription(S["Retrieves the definitions of all available content types."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddCoreAITool<GetContentPartDefinitionsTool>(GetContentPartDefinitionsTool.TheName)
            .WithTitle(S["Get Content Part Definitions"])
            .WithDescription(S["Retrieves the definitions of all available content parts."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddCoreAITool<ListContentTypesDefinitionsTool>(ListContentTypesDefinitionsTool.TheName)
            .WithTitle(S["List Available Content Types Definitions"])
            .WithDescription(S["Provides a list of available content types definitions."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddCoreAITool<ListContentPartsDefinitionsTool>(ListContentPartsDefinitionsTool.TheName)
            .WithTitle(S["List Available Content Parts Definitions"])
            .WithDescription(S["Provides a list of available content parts definitions."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddCoreAITool<ListContentFieldsTool>(ListContentFieldsTool.TheName)
            .WithTitle(S["List Available Content Fields"])
            .WithDescription(S["Provides a list of available content fields."])
            .WithCategory(S["Content Definitions"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the ContentDefinitionRecipesTools feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.ContentTypes", "OrchardCore.Recipes.Core")]
public sealed class ContentDefinitionRecipesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDefinitionRecipesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentDefinitionRecipesToolsStartup(IStringLocalizer<ContentDefinitionRecipesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<RemoveContentTypeDefinitionsTool>(RemoveContentTypeDefinitionsTool.TheName)
            .WithTitle(S["Remove Content Type Definitions"])
            .WithDescription(S["Removes the content type definition."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddCoreAITool<RemoveContentPartDefinitionsTool>(RemoveContentPartDefinitionsTool.TheName)
            .WithTitle(S["Remove Content Part Definitions"])
            .WithDescription(S["Removes the content part definition."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddCoreAITool<CreateOrUpdateContentTypeDefinitionsTool>(CreateOrUpdateContentTypeDefinitionsTool.TheName)
            .WithTitle(S["Create or Update Content Type Definition"])
            .WithDescription(S["Creates a new content type definition or updates an existing one."])
            .WithCategory(S["Content Definitions"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Features feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Features")]
public sealed class FeaturesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeaturesStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FeaturesStartup(IStringLocalizer<FeaturesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<DisableFeatureTool>(DisableFeatureTool.TheName)
            .WithTitle(S["Disable Site Feature"])
            .WithDescription(S["Disabled site features."])
            .WithCategory(S["Features Management"])
            .Selectable();

        services.AddCoreAITool<EnableFeatureTool>(EnableFeatureTool.TheName)
            .WithTitle(S["Enable Site Feature"])
            .WithDescription(S["Enable site features."])
            .WithCategory(S["Features Management"])
            .Selectable();

        services.AddCoreAITool<FeaturesSearchTool>(FeaturesSearchTool.TheName)
            .WithTitle(S["Search for Site Feature"])
            .WithDescription(S["Search available features for a match."])
            .WithCategory(S["Features Management"])
            .Selectable();

        services.AddCoreAITool<ListFeaturesTool>(ListFeaturesTool.TheName)
            .WithTitle(S["List Site Features"])
            .WithDescription(S["Retrieves available site features."])
            .WithCategory(S["Features Management"])
            .Selectable();

        services.AddCoreAITool<GetFeatureTool>(GetFeatureTool.TheName)
            .WithTitle(S["Get Site Features"])
            .WithDescription(S["Retrieves info about a feature."])
            .WithCategory(S["Features Management"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Notifications feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Notifications")]
public sealed class NotificationsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NotificationsStartup(IStringLocalizer<NotificationsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SendNotificationTool>(SendNotificationTool.TheName)
            .WithTitle(S["Send User Notification"])
            .WithDescription(S["Sends a notification message to a user."])
            .WithCategory(S["Communications"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Email feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Email")]
public sealed class EmailStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmailStartup(IStringLocalizer<EmailStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SendEmailTool>(SendEmailTool.TheName)
            .WithTitle(S["Send Emails"])
            .WithDescription(S["Sends a email message on the behalf of the logged user."])
            .WithCategory(S["Communications"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Sms feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Sms")]
public sealed class SmsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsStartup(IStringLocalizer<SmsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SendSmsTool>(SendSmsTool.TheName)
            .WithTitle(S["Send SMS message"])
            .WithDescription(S["Sends a SMS message to a user."])
            .WithCategory(S["Communications"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Users feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Users")]
public sealed class UsersStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UsersStartup(IStringLocalizer<UsersStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetUserInfoTool>(GetUserInfoTool.TheName)
            .WithTitle(S["Get User Info"])
            .WithDescription(S["Gets information about a user."])
            .WithCategory(S["Users Management"])
            .Selectable();

        services.AddCoreAITool<SearchForUsersTool>(SearchForUsersTool.TheName)
            .WithTitle(S["Search Users"])
            .WithDescription(S["Search the system for users."])
            .WithCategory(S["Users Management"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Roles feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Roles")]
public sealed class RolesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RolesStartup(IStringLocalizer<RolesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetRoleTool>(GetRoleTool.TheName)
            .WithTitle(S["Get Role Info"])
            .WithDescription(S["Gets information about a role."])
            .WithCategory(S["Roles Management"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Workflows feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkflowsStartup(IStringLocalizer<WorkflowsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetWorkflowTypesTool>(GetWorkflowTypesTool.TheName)
            .WithTitle(S["Get Workflow Type Info"])
            .WithDescription(S["Gets information about a workflow type."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddCoreAITool<ListWorkflowTypesTool>(ListWorkflowTypesTool.TheName)
            .WithTitle(S["List Workflow Type"])
            .WithDescription(S["List information about a workflow types."])
            .WithCategory(S["Workflow Management"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the WorkflowsRecipes feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Workflows", "OrchardCore.Recipes.Core")]
public sealed class WorkflowsRecipesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowsRecipesStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkflowsRecipesStartup(IStringLocalizer<WorkflowsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<CreateOrUpdateWorkflowTool>(CreateOrUpdateWorkflowTool.TheName)
            .WithTitle(S["Create Workflows"])
            .WithDescription(S["Create or update information a workflow."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddCoreAITool<ListWorkflowActivitiesTool>(ListWorkflowActivitiesTool.TheName)
            .WithTitle(S["List Workflow Activities"])
            .WithDescription(S["List all available tasks and activities a workflow."])
            .WithCategory(S["Workflow Management"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the Profiles feature.
/// </summary>
[Feature(AIConstants.Feature.OrchardCoreAIAgent)]
public sealed class ProfilesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfilesStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProfilesStartup(IStringLocalizer<ProfilesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAIProfilesTool>(ListAIProfilesTool.TheName)
            .WithTitle(S["List AI Profiles"])
            .WithDescription(S["Lists AI profiles with optional filters for type, analytics, data extraction, and post-session processing."])
            .WithCategory(S["AI Profiles"])
            .Selectable();

        services.AddCoreAITool<ViewAIProfileTool>(ViewAIProfileTool.TheName)
            .WithTitle(S["View AI Profile"])
            .WithDescription(S["Retrieves detailed configuration for a specific AI profile by ID or name."])
            .WithCategory(S["AI Profiles"])
            .Selectable();
    }
}

/// <summary>
/// Registers services and configuration for the ChatAnalyticsTools feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, AIConstants.Feature.ChatAnalytics)]
public sealed class ChatAnalyticsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatAnalyticsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatAnalyticsToolsStartup(IStringLocalizer<ChatAnalyticsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<QueryChatSessionMetricsTool>(QueryChatSessionMetricsTool.TheName)
            .WithTitle(S["Query Chat Session Metrics"])
            .WithDescription(S["Queries aggregated chat session analytics metrics with optional date range and profile filters. Returns statistics for generating charts and reports."])
            .WithCategory(S["AI Analytics"])
            .Selectable();
    }
}
