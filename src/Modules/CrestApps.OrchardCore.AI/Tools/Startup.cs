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
using CrestApps.OrchardCore.AI.Tools.Recipes;
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
        services.AddDisplayDriver<AIToolInstance, InvokableToolMetadataDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, AIProfileToolMetadataDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, AIToolInstanceDisplayDriver>();
        services.AddDisplayDriver<AIProfile, AIProfileToolsDisplayDriver>();
        services.AddNavigationProvider<AIToolInstancesAdminMenu>();
        services.AddPermissionProvider<AIToolPermissionProvider>();

        services.AddAIToolSource<ProfileAwareAIToolSource>(ProfileAwareAIToolSource.ToolSource);
        services.AddScoped<IAICompletionServiceHandler, FunctionInvocationAICompletionServiceHandler>();
    }
}

[Feature(AIConstants.Feature.LocalTools)]
public sealed class LocalToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public LocalToolsStartup(IStringLocalizer<LocalToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<ImportOrchardCoreRecipeTool>(ImportOrchardCoreRecipeTool.TheName, (o) =>
        {
            o.Title = S["Import Orchard Core Recipes"];
            o.Description = S["Provides AI Agents for your site."];
        });
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

        services.AddAITool<ApplySystemConfigurationsCoreRecipeTool>(ApplySystemConfigurationsCoreRecipeTool.TheName, (o) =>
        {
            o.Title = S["Apply site settings"];
            o.Description = S["Provides capability to apply site settings."];
        });

        services.AddAITool<GetNonStartupRecipesOrchardCoreTool>(GetNonStartupRecipesOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Get non-startup recipes."];
            o.Description = S["Provides a list of non-startup recipes."];
        });

        services.AddAITool<GetNonStartupRecipesOrchardCoreTool>(GetNonStartupRecipesOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Get non-startup recipes."];
            o.Description = S["Provides a list of non-startup recipes."];
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
        services.AddAITool<GetStartupRecipesOrchardCoreTool>(GetNonStartupRecipesOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Get startup recipes."];
            o.Description = S["Provides a list of startup recipes."];
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
        services.AddAITool<PublishContentOrchardCoreTool>(PublishContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Publish Content Item"];
            o.Description = S["Allows publishing content item."];
        });

        services.AddAITool<UnpublishContentOrchardCoreTool>(UnpublishContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Unpublish Content Item"];
            o.Description = S["Allows unpublishing content item."];
        });

        services.AddAITool<GetContentOrchardCoreTool>(GetContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Get Content Item"];
            o.Description = S["Allows retrieving content item."];
        });

        services.AddAITool<DeleteContentOrchardCoreTool>(DeleteContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Delete Content Item"];
            o.Description = S["Allows deleting content item."];
        });

        services.AddAITool<CloneContentOrchardCoreTool>(CloneContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Clone Content Item"];
            o.Description = S["Allows cloning content item."];
        });

        services.AddAITool<CreateOrUpdateContentOrchardCoreTool>(CreateOrUpdateContentOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Create or Update Content Item"];
            o.Description = S["Allows creating or updating content items."];
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
            o.Title = S["Get Content type definition"];
            o.Description = S["Allows retrieving content type definition."];
        });

        services.AddAITool<CreateOrUpdateContentTypeDefinitionsOrchardCoreTool>(CreateOrUpdateContentTypeDefinitionsOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Create or update Content type definition"];
            o.Description = S["Allows retrieving content type definition."];
        });

        services.AddAITool<GetContentPartDefinitionsOrchardCoreTool>(GetContentPartDefinitionsOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Get Content part definition"];
            o.Description = S["Allows retrieving content part definition."];
        });

        services.AddAITool<CreateOrUpdateContentPartDefinitionsOrchardCoreTool>(CreateOrUpdateContentPartDefinitionsOrchardCoreTool.TheName, (o) =>
        {
            o.Title = S["Create or update Content part definition"];
            o.Description = S["Allows creating or updating content part definition."];
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

