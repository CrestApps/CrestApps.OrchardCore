using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center Inbound Voice feature: inbound voice entry-point administration, qualification,
/// and queue ingress.
/// </summary>
[Feature(ContactCenterConstants.Feature.InboundVoice)]
public sealed class InboundVoiceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterEntryPointStore, ContactCenterEntryPointStore>()
            .AddScoped<IContactCenterEntryPointManager, ContactCenterEntryPointManager>()
            .AddScoped<IEntryPointResolver, EntryPointResolver>()
            .AddScoped<IQueuedVoiceWorkOfferService, QueuedVoiceWorkOfferService>()
            .AddScoped<IPendingIncomingCallOfferService, PendingIncomingCallOfferService>()
            .AddScoped<QueuedVoiceWorkOfferScopeContext>()
            .AddScoped<IContactCenterEventHandler, OfferQueuedVoiceWorkOnAvailabilityHandler>()
            .AddScoped<ICatalogEntryHandler<ContactCenterEntryPoint>, ContactCenterEntryPointHandler>()
            .AddScoped<ICatalogEntryHandler<ContactCenterEntryPoint>, ContactCenterConfigurationCacheInvalidationHandler<ContactCenterEntryPoint>>()
            .AddIndexProvider<ContactCenterEntryPointIndexProvider>()
            .AddDataMigration<ContactCenterEntryPointIndexMigrations>();

        // Inbound entry-point administration screens.
        services.AddDisplayDriver<ContactCenterEntryPoint, ContactCenterEntryPointDisplayDriver>();
        services.AddNavigationProvider<ContactCenterEntryPointsAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddVoiceIngressEndpoint();
    }
}

/// <summary>
/// Registers the deployment steps that export the entry points owned by the entry points feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.InboundVoice)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class EntryPointsDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterEntryPointDeploymentSource, ContactCenterEntryPointDeploymentStep>();
    }
}

/// <summary>
/// Registers the recipe steps that import the entry points owned by the entry points feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.InboundVoice)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class EntryPointsRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterEntryPointStep>();
    }
}
