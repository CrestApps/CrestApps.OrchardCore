using CrestApps.OrchardCore.AI.Agents.Contents;
using CrestApps.OrchardCore.AI.Agents.ContentTypes;
using CrestApps.OrchardCore.AI.Agents.Features;
using CrestApps.OrchardCore.AI.Agents.Recipes;
using CrestApps.OrchardCore.AI.Agents.System;
using CrestApps.OrchardCore.AI.Agents.Tenants;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agents;

[Feature(AIConstants.Feature.Agents)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}

[RequireFeatures(AIConstants.Feature.Agents, "OrchardCore.Recipes.Core")]
public sealed class RecipesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public RecipesToolsStartup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<ImportOrchardTool>(ImportOrchardTool.TheName, (o) =>
        {
            o.Title = S["Import Orchard Core Recipe"];
            o.Description = S["Enables AI agents to import and run Orchard Core recipes within your site."];
            o.Category = S["Recipes"];
        });

        services.AddAITool<ApplySystemSettingsTool>(ApplySystemSettingsTool.TheName, (o) =>
        {
            o.Title = S["Apply Site Configuration"];
            o.Description = S["Applies predefined system configurations and settings using AI assistance."];
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

[RequireFeatures(AIConstants.Feature.Agents, "OrchardCore.Tenants")]
public sealed class TenantsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public TenantsToolsStartup(IStringLocalizer<Startup> stringLocalizer)
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
    }
}

[RequireFeatures(AIConstants.Feature.Agents, "OrchardCore.Contents")]
public sealed class ContentsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentsToolsStartup(IStringLocalizer<ContentsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
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
            o.Description = S["Retrieves a link for a content type."];
            o.Category = S["Content Management"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.Agents, "OrchardCore.ContentTypes", "OrchardCore.Recipes.Core")]
public sealed class ContentTypesRecipesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentTypesRecipesToolsStartup(IStringLocalizer<ContentTypesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<CreateOrUpdateContentTypeDefinitionsTool>(CreateOrUpdateContentTypeDefinitionsTool.TheName, (o) =>
        {
            o.Title = S["Create or Update Content Type Definition"];
            o.Description = S["Creates a new content type definition or updates an existing one."];
            o.Category = S["Content Definitions"];
        });

        services.AddAITool<CreateOrUpdateContentPartDefinitionsTool>(CreateOrUpdateContentPartDefinitionsTool.TheName, (o) =>
        {
            o.Title = S["Create or Update Content Part Definition"];
            o.Description = S["Creates a new content part definition or updates an existing one."];
            o.Category = S["Content Definitions"];
        });
    }
}

[RequireFeatures(AIConstants.Feature.Agents, "OrchardCore.ContentTypes")]
public sealed class ContentTypesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentTypesToolsStartup(IStringLocalizer<ContentTypesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
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
    }
}

[RequireFeatures(AIConstants.Feature.Agents, "OrchardCore.Features")]
public sealed class FeaturesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public FeaturesToolsStartup(IStringLocalizer<ContentTypesToolsStartup> stringLocalizer)
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


