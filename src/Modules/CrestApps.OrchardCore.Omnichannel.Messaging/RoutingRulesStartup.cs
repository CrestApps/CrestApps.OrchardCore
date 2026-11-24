using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Sources;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;
using CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Teams;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Registers the messaging routing rules: their catalog and admin pages, the router that tries them on a contact's
/// message, the hook on automated sends, the sweep that retries waiting conversations, and their recipe and deployment
/// steps.
/// </summary>
[Feature(MessagingConstants.Feature.RoutingRules)]
public sealed class RoutingRulesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddYesSqlDocumentCatalog<MessagingRoutingRule, MessagingRoutingRuleIndex>(collection: MessagingStorage.CollectionName)
            .AddScoped<ICatalogEntryHandler<MessagingRoutingRule>, MessagingRoutingRuleHandler>()
            .AddIndexProvider<MessagingRoutingRuleIndexProvider>()
            .AddDataMigration<MessagingRoutingRuleIndexMigrations>();

        // The rules pick through team assignment, which balances the agents' messaging work these sources report.
        services
            .AddScoped<MessagingRoutingLoadSnapshot>()
            .AddSingleton<IMessagingRoutingLedger, DistributedCacheMessagingRoutingLedger>()
            .AddTeamMemberLoadSource<MessagingConversationLoadSource>()
            .AddTeamMemberLoadSource<MessagingWeightedConversationLoadSource>();

        services
            .AddScoped<MessagingRoutingCandidateResolver>()
            .AddScoped<IMessagingRoutingRuleEvaluator, MessagingRoutingRuleEvaluator>()
            .AddScoped<IMessagingInboundRouter, RoutingRulesRouter>()
            .AddScoped<MessagingRoutingRulesSweep>();

        services.AddSingleton<IBackgroundTask, MessagingRoutingRulesBackgroundTask>();

        services.AddDisplayDriver<MessagingRoutingRule, MessagingRoutingRuleDisplayDriver>();
        services.AddNavigationProvider<MessagingRoutingRulesAdminMenu>();
        services.AddPermissionProvider<MessagingRoutingRulesPermissionProvider>();

        services.AddRecipeExecutionStep<MessagingRoutingRulesStep>();
        services.AddScoped<IRecipeStep, MessagingRoutingRulesRecipeStepSchema>();
        services.AddDeployment<MessagingRoutingRulesDeploymentSource, MessagingRoutingRulesDeploymentStep, SummaryDeploymentStepDisplayDriver<MessagingRoutingRulesDeploymentStep>>();
        services.AddDeploymentStepSchema<MessagingRoutingRulesDeploymentStepSchema>();
    }
}
