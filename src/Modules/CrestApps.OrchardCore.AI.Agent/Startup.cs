using CrestApps.OrchardCore.AI.Agent.Communications;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.Features;
using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Agent.Roles;
using CrestApps.OrchardCore.AI.Agent.Schemas;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Agent.System;
using CrestApps.OrchardCore.AI.Agent.Tenants;
using CrestApps.OrchardCore.AI.Agent.Users;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

[Feature(AIConstants.Feature.OrchardCoreAIAgent)]
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<ListTimeZoneTool>(ListTimeZoneTool.TheName, (o) =>
        {
            o.Title = S["List System Time Zones"];
            o.Description = S["Retrieves a list of the available time zones in the system."];
            o.Category = S["System"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public RecipesStartup(IStringLocalizer<RecipesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, SettingsSchemaStep>();

        services.AddAITool<ApplySystemSettingsTool>(ApplySystemSettingsTool.TheName, (o) =>
        {
            o.Title = S["Apply Site Configuration"];
            o.Description = S["Applies predefined system configurations and settings using AI assistance."];
            o.Category = S["Recipes"];
        });

        services.AddAITool<ImportOrchardTool>(ImportOrchardTool.TheName, (o) =>
        {
            o.Title = S["Import Orchard Core Recipe"];
            o.Description = S["Enables AI agents to import and run Orchard Core recipes within your site."];
            o.Category = S["Recipes"];
        });

        services.AddAITool<ListNonStartupRecipesTool>(ListNonStartupRecipesTool.TheName, (o) =>
        {
            o.Title = S["List Non-Startup Recipes"];
            o.Description = S["Retrieves all available Orchard Core recipes that are not executed during startup."];
            o.Category = S["Recipes"];
        });

        services.AddAITool<ExecuteStartupRecipesTool>(ExecuteStartupRecipesTool.TheName, (o) =>
        {
            o.Title = S["Run Non-Startup Recipes"];
            o.Description = S["Executes Orchard Core recipes that are not configured to run at application startup."];
            o.Category = S["Recipes"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Tenants")]
public sealed class TenantsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public TenantsStartup(IStringLocalizer<TenantsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<ListStartupRecipesTool>(ListStartupRecipesTool.TheName, (o) =>
        {
            o.Title = S["List Startup Recipes"];
            o.Description = S["Retrieves a list of Orchard Core recipes configured to run at application startup."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<CreateTenantTool>(CreateTenantTool.TheName, (o) =>
        {
            o.Title = S["Create Tenant"];
            o.Description = S["Creates a new tenant in the Orchard Core application."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<GetTenantTool>(GetTenantTool.TheName, (o) =>
        {
            o.Title = S["Get Tenant Information"];
            o.Description = S["Retrieves detailed information about a specific tenant."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<ListTenantTool>(ListTenantTool.TheName, (o) =>
        {
            o.Title = S["List All Tenants"];
            o.Description = S["Returns information about all tenants in the system."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<EnableTenantTool>(EnableTenantTool.TheName, (o) =>
        {
            o.Title = S["Enable Tenant"];
            o.Description = S["Enables a tenant that is currently disabled."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<DisableTenantTool>(DisableTenantTool.TheName, (o) =>
        {
            o.Title = S["Disable Tenant"];
            o.Description = S["Disables a tenant that is currently active."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<RemoveTenantTool>(RemoveTenantTool.TheName, (o) =>
        {
            o.Title = S["Remove Tenant"];
            o.Description = S["Removes an existing tenant that can be safely deleted."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<ReloadTenantTool>(ReloadTenantTool.TheName, (o) =>
        {
            o.Title = S["Reload Tenant"];
            o.Description = S["Reloads the configuration and state of an existing tenant."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<SetupTenantTool>(SetupTenantTool.TheName, (o) =>
        {
            o.Title = S["Setup Tenant"];
            o.Description = S["Sets up new tenants."];
            o.Category = S["Tenants Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Contents")]
public sealed class ContentsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentsStartup(IStringLocalizer<ContentsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, CommonPartDefinitionSchemaDefinition>();

        services.AddAITool<SearchForContentsTool>(SearchForContentsTool.TheName, (o) =>
        {
            o.Title = S["Search Content Items"];
            o.Description = S["Provides a way to search for content items."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<GetContentItemSchemaTool>(GetContentItemSchemaTool.TheName, (o) =>
        {
            o.Title = S["Generate Content Item Sample"];
            o.Description = S["Generates a structured sample content item for a specified content type."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<PublishContentTool>(PublishContentTool.TheName, (o) =>
        {
            o.Title = S["Publish Content Item"];
            o.Description = S["Publishes a draft or previously unpublished content item."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<UnpublishContentTool>(UnpublishContentTool.TheName, (o) =>
        {
            o.Title = S["Unpublish Content Item"];
            o.Description = S["Unpublishes a currently published content item."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<GetContentTool>(GetContentTool.TheName, (o) =>
        {
            o.Title = S["Retrieve Content Item"];
            o.Description = S["Retrieves a specific content item by its ID or type."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<DeleteContentTool>(DeleteContentTool.TheName, (o) =>
        {
            o.Title = S["Delete Content Item"];
            o.Description = S["Deletes a content item from the system."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<CloneContentTool>(CloneContentTool.TheName, (o) =>
        {
            o.Title = S["Clone Content Item"];
            o.Description = S["Creates a duplicate of an existing content item."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<CreateOrUpdateContentTool>(CreateOrUpdateContentTool.TheName, (o) =>
        {
            o.Title = S["Create or Update Content Item"];
            o.Description = S["Creates a new content item or updates an existing one."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<GetContentItemLinkTool>(GetContentItemLinkTool.TheName, (o) =>
        {
            o.Title = S["Retrieve a Link for a Content Item"];
            o.Description = S["Retrieves a link for a content item."];
            o.Category = S["Content Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.ContentTypes")]
public sealed class ContentDefinitionsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentDefinitionsStartup(IStringLocalizer<ContentDefinitionsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ContentMetadataService>();

        services.AddAITool<GetContentTypeDefinitionsTool>(GetContentTypeDefinitionsTool.TheName, (o) =>
        {
            o.Title = S["Get Content Type Definitions"];
            o.Description = S["Retrieves the definitions of all available content types."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<GetContentPartDefinitionsTool>(GetContentPartDefinitionsTool.TheName, (o) =>
        {
            o.Title = S["Get Content Part Definitions"];
            o.Description = S["Retrieves the definitions of all available content parts."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<ListContentTypesDefinitionsTool>(ListContentTypesDefinitionsTool.TheName, (o) =>
        {
            o.Title = S["List Available Content Types Definitions"];
            o.Description = S["Provides a list of available content types definitions."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<ListContentPartsDefinitionsTool>(ListContentPartsDefinitionsTool.TheName, (o) =>
        {
            o.Title = S["List Available Content Parts Definitions"];
            o.Description = S["Provides a list of available content parts definitions."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<ListContentFieldsTool>(ListContentFieldsTool.TheName, (o) =>
        {
            o.Title = S["List Available Content Fields"];
            o.Description = S["Provides a list of available content fields."];
            o.Category = S["Content Definitions"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.ContentTypes", "OrchardCore.Recipes.Core")]
public sealed class ContentDefinitionRecipesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentDefinitionRecipesToolsStartup(IStringLocalizer<ContentDefinitionRecipesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ContentDefinitionSchemaStep>();

        services.AddAITool<RemoveContentTypeDefinitionsTool>(RemoveContentTypeDefinitionsTool.TheName, (o) =>
        {
            o.Title = S["Remove Content Type Definitions"];
            o.Description = S["Removes the content type definition."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<RemoveContentPartDefinitionsTool>(RemoveContentPartDefinitionsTool.TheName, (o) =>
        {
            o.Title = S["Remove Content Part Definitions"];
            o.Description = S["Removes the content part definition."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<CreateOrUpdateContentTypeDefinitionsTool>(CreateOrUpdateContentTypeDefinitionsTool.TheName, (o) =>
        {
            o.Title = S["Create or Update Content Type Definition"];
            o.Description = S["Creates a new content type definition or updates an existing one."];
            o.Category = S["Content Definitions"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Features")]
public sealed class FeaturesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public FeaturesStartup(IStringLocalizer<FeaturesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<DisableFeatureTool>(DisableFeatureTool.TheName, (o) =>
        {
            o.Title = S["Disable Site Feature"];
            o.Description = S["Disabled site features."];
            o.Category = S["Features Management"];
        });

        services.AddAITool<EnableFeatureTool>(EnableFeatureTool.TheName, (o) =>
        {
            o.Title = S["Enable Site Feature"];
            o.Description = S["Enable site features."];
            o.Category = S["Features Management"];
        });

        services.AddAITool<FeaturesSearchTool>(FeaturesSearchTool.TheName, (o) =>
        {
            o.Title = S["Search for Site Feature"];
            o.Description = S["Search available features for a match."];
            o.Category = S["Features Management"];
        });

        services.AddAITool<ListFeaturesTool>(ListFeaturesTool.TheName, (o) =>
        {
            o.Title = S["List Site Features"];
            o.Description = S["Retrieves available site features."];
            o.Category = S["Features Management"];
        });

        services.AddAITool<GetFeatureTool>(GetFeatureTool.TheName, (o) =>
        {
            o.Title = S["Get Site Features"];
            o.Description = S["Retrieves info about a feature."];
            o.Category = S["Features Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Notifications")]
public sealed class NotificationsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public NotificationsStartup(IStringLocalizer<NotificationsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<SendNotificationTool>(SendNotificationTool.TheName, (o) =>
        {
            o.Title = S["Send User Notification"];
            o.Description = S["Sends a notification message to a user."];
            o.Category = S["Communications"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Email")]
public sealed class EmailStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public EmailStartup(IStringLocalizer<EmailStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<SendEmailTool>(SendEmailTool.TheName, (o) =>
        {
            o.Title = S["Send Emails"];
            o.Description = S["Sends a email message on the behalf of the logged user."];
            o.Category = S["Communications"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Sms")]
public sealed class SmsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public SmsStartup(IStringLocalizer<SmsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<SendSmsTool>(SendSmsTool.TheName, (o) =>
        {
            o.Title = S["Send SMS message"];
            o.Description = S["Sends a SMS message to a user."];
            o.Category = S["Communications"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Users")]
public sealed class UsersStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public UsersStartup(IStringLocalizer<UsersStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetUserTool>(GetUserTool.TheName, (o) =>
        {
            o.Title = S["Get User Info"];
            o.Description = S["Gets information about a user."];
            o.Category = S["Users Management"];
        });

        services.AddAITool<SearchForUsersTool>(SearchForUsersTool.TheName, (o) =>
        {
            o.Title = S["Search Users"];
            o.Description = S["Search the system for users."];
            o.Category = S["Users Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Roles")]
public sealed class RolesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public RolesStartup(IStringLocalizer<RolesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetRoleTool>(GetRoleTool.TheName, (o) =>
        {
            o.Title = S["Get Role Info"];
            o.Description = S["Gets information about a role."];
            o.Category = S["Roles Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public WorkflowsStartup(IStringLocalizer<WorkflowsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetWorkflowTypesTool>(GetWorkflowTypesTool.TheName, (o) =>
        {
            o.Title = S["Get Workflow Type Info"];
            o.Description = S["Gets information about a workflow type."];
            o.Category = S["Workflow Management"];
        });

        services.AddAITool<ListWorkflowTypesTool>(ListWorkflowTypesTool.TheName, (o) =>
        {
            o.Title = S["List Workflow Type"];
            o.Description = S["List information about a workflow types."];
            o.Category = S["Workflow Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Workflows", "OrchardCore.Recipes.Core")]
public sealed class WorkflowsRecipesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public WorkflowsRecipesStartup(IStringLocalizer<WorkflowsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, WorkflowTypeSchemaStep>();

        services.AddAITool<CreateOrUpdateWorkflowTool>(CreateOrUpdateWorkflowTool.TheName, (o) =>
        {
            o.Title = S["Create Workflows"];
            o.Description = S["Create or update information a workflow."];
            o.Category = S["Workflow Management"];
        });

        services.AddAITool<ListWorkflowActivitiesTool>(ListWorkflowActivitiesTool.TheName, (o) =>
        {
            o.Title = S["List Workflow Activities"];
            o.Description = S["List all available tasks and activities a workflow."];
            o.Category = S["Workflow Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Title")]
public sealed class TitleStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, TitlePartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Autoroute")]
public sealed class AutorouteStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, AutoroutePartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Alias")]
public sealed class AliasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, AliasPartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Html")]
public sealed class HtmlStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, HtmlBodyPartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Markdown")]
public sealed class MarkdownStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, MarkdownBodyPartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.List")]
public sealed class ListStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, ListPartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Flows")]
public sealed class FlowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, FlowPartDefinitionSchemaDefinition>();
        services.AddScoped<IContentDefinitionSchemaDefinition, BagPartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Widgets")]
public sealed class WidgetsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, WidgetsListPartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.ContentPreview")]
public sealed class ContentPreviewStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, PreviewPartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Seo")]
public sealed class SeoStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, SeoMetaPartDefinitionSchemaDefinition>();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.AuditTrail")]
public sealed class AuditTrailStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDefinitionSchemaDefinition, AuditTrailPartDefinitionSchemaDefinition>();
    }
}
