using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Deployments.Drivers;
using CrestApps.OrchardCore.AI.Deployments.Sources;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Recipes;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.Tools.Contents;
using CrestApps.OrchardCore.AI.Tools.ContentTypes;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using CrestApps.OrchardCore.AI.Tools.Features;
using CrestApps.OrchardCore.AI.Tools.Recipes;
using CrestApps.OrchardCore.AI.Tools.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Tools;

[Feature(AIConstants.Feature.AITools)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIProfile, AIProfileToolInstancesDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, InvokableToolMetadataDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, AIProfileToolMetadataDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, AIToolInstanceDisplayDriver>();
        services.AddNavigationProvider<AIToolInstancesAdminMenu>();
        services.AddPermissionProvider<AIToolPermissionProvider>();

        services.AddAIToolSource<ProfileAwareAIToolSource>(ProfileAwareAIToolSource.ToolSource);
        services.AddScoped<IAICompletionServiceHandler, FunctionInstancesAICompletionServiceHandler>();
    }
}

[Feature(AIConstants.Feature.LocalTools)]
public sealed class LocalToolsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}

[RequireFeatures(AIConstants.Feature.LocalTools, "OrchardCore.Recipes.Core")]
public sealed class RecipesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public RecipesToolsStartup(IStringLocalizer<LocalToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIToolInstanceStep>();

        services.AddAITool<ImportOrchardCoreRecipeTool>(ImportOrchardCoreRecipeTool.TheName, (o) =>
        {
            o.Title = S["Import Orchard Core Recipe"];
            o.Description = S["Enables AI agents to import and run Orchard Core recipes within your site."];
            o.Category = S["Recipes"];
        });

        services.AddAITool<ApplySystemConfigurationsCoreRecipeTool>(ApplySystemConfigurationsCoreRecipeTool.TheName, (o) =>
        {
            o.Title = S["Apply Site Configuration"];
            o.Description = S["Applies predefined system configurations and settings using AI assistance."];
            o.Category = S["Recipes"];
        });

        services.AddAITool<GetNonStartupRecipesOrchardCoreTool>(GetNonStartupRecipesOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["List Non-Startup Recipes"];
            o.Description = S["Retrieves all available Orchard Core recipes that are not executed during startup."];
            o.Category = S["Recipes"];
        });

        services.AddAITool<ExecuteStartupRecipesOrchardCoreTool>(ExecuteStartupRecipesOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Run Non-Startup Recipes"];
            o.Description = S["Executes Orchard Core recipes that are not configured to run at application startup."];
            o.Category = S["Recipes"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.LocalTools, "OrchardCore.Tenants")]
public sealed class TenantsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public TenantsToolsStartup(IStringLocalizer<LocalToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetStartupRecipesOrchardCoreTool>(GetStartupRecipesOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["List Startup Recipes"];
            o.Description = S["Retrieves a list of Orchard Core recipes configured to run at application startup."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<CreateTenantOrchardCoreTool>(CreateTenantOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Create Tenant"];
            o.Description = S["Creates a new tenant in the Orchard Core application."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<GetTenantOrchardCoreTool>(GetTenantOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Get Tenant Information"];
            o.Description = S["Retrieves detailed information about a specific tenant."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<ListTenantOrchardCoreTool>(ListTenantOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["List All Tenants"];
            o.Description = S["Returns information about all tenants in the system."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<EnableTenantOrchardCoreTool>(EnableTenantOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Enable Tenant"];
            o.Description = S["Enables a tenant that is currently disabled."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<DisableTenantOrchardCoreTool>(DisableTenantOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Disable Tenant"];
            o.Description = S["Disables a tenant that is currently active."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<RemoveTenantOrchardCoreTool>(RemoveTenantOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Remove Tenant"];
            o.Description = S["Removes an existing tenant that can be safely deleted."];
            o.Category = S["Tenants Management"];
        });

        services.AddAITool<ReloadTenantOrchardCoreTool>(ReloadTenantOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Reload Tenant"];
            o.Description = S["Reloads the configuration and state of an existing tenant."];
            o.Category = S["Tenants Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.LocalTools, "OrchardCore.Contents")]
public sealed class ContentsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentsToolsStartup(IStringLocalizer<ContentsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetContentItemSchemaOrchardCoreTool>(GetContentItemSchemaOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Generate Content Item Sample"];
            o.Description = S["Generates a structured sample content item for a specified content type."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<PublishContentOrchardCoreTool>(PublishContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Publish Content Item"];
            o.Description = S["Publishes a draft or previously unpublished content item."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<UnpublishContentOrchardCoreTool>(UnpublishContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Unpublish Content Item"];
            o.Description = S["Unpublishes a currently published content item."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<GetContentOrchardCoreTool>(GetContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Retrieve Content Item"];
            o.Description = S["Retrieves a specific content item by its ID or type."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<DeleteContentOrchardCoreTool>(DeleteContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Delete Content Item"];
            o.Description = S["Deletes a content item from the system."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<CloneContentOrchardCoreTool>(CloneContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Clone Content Item"];
            o.Description = S["Creates a duplicate of an existing content item."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<CreateOrUpdateContentOrchardCoreTool>(CreateOrUpdateContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Create or Update Content Item"];
            o.Description = S["Creates a new content item or updates an existing one."];
            o.Category = S["Content Management"];
        });

        services.AddAITool<GetContentItemLinkOrchardCoreTool>(GetContentItemLinkOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Retrieve a Link for a Content Item"];
            o.Description = S["Retrieves a link for a content type."];
            o.Category = S["Content Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.LocalTools, "OrchardCore.ContentTypes")]
public sealed class ContentTypesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentTypesToolsStartup(IStringLocalizer<ContentTypesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetContentTypeDefinitionsOrchardCoreTool>(GetContentTypeDefinitionsOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Get Content Type Definitions"];
            o.Description = S["Retrieves the definitions of all available content types."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<CreateOrUpdateContentTypeDefinitionsOrchardCoreTool>(CreateOrUpdateContentTypeDefinitionsOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Create or Update Content Type Definition"];
            o.Description = S["Creates a new content type definition or updates an existing one."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<GetContentPartDefinitionsOrchardCoreTool>(GetContentPartDefinitionsOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Get Content Part Definitions"];
            o.Description = S["Retrieves the definitions of all available content parts."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<CreateOrUpdateContentPartDefinitionsOrchardCoreTool>(CreateOrUpdateContentPartDefinitionsOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Create or Update Content Part Definition"];
            o.Description = S["Creates a new content part definition or updates an existing one."];
            o.Category = S["Content Definitions"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.LocalTools, "OrchardCore.Features")]
public sealed class FeaturesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public FeaturesToolsStartup(IStringLocalizer<ContentTypesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<DisableFeatureOrchardCoreTool>(DisableFeatureOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Disable Site Feature"];
            o.Description = S["Disabled site features."];
            o.Category = S["Features Management"];
        });

        services.AddAITool<EnableFeatureOrchardCoreTool>(EnableFeatureOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Enable Site Feature"];
            o.Description = S["Enable site features."];
            o.Category = S["Features Management"];
        });

        services.AddAITool<FeaturesSearchOrchardCoreTool>(FeaturesSearchOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Search for Site Feature"];
            o.Description = S["Search available features for a match."];
            o.Category = S["Features Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.AITools, "OrchardCore.Deployment")]
public sealed class ToolOCDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIToolInstanceDeploymentSource, AIToolInstanceDeploymentStep, AIToolInstanceDeploymentStepDisplayDriver>();
    }
}

