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
using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
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
    private readonly IStringLocalizer S;

    public InboundVoiceStartup(IStringLocalizer<InboundVoiceStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // A phone number channel endpoint only has an inbound handler when inbound voice is enabled (it maps a
        // dialed number to a subject flow), so the Phone channel is offered in the channel-endpoint create picker
        // only with this feature. When the channel-endpoint administration is also enabled, Phone appears there.
        services.TryAddScoped<IIvrProvider, NoIvrProvider>();

        services.AddChannelEndpointSource(OmnichannelConstants.Channels.Phone, source =>
        {
            source.DisplayName = S["Phone"];
            source.Description = S["A phone number for inbound voice. Routes a dialed number to a subject flow."];
        });

        services
            .AddScoped<IContactCenterEntryPointStore, ContactCenterEntryPointStore>()
            .AddScoped<IContactCenterEntryPointManager, ContactCenterEntryPointManager>()
            // Entry-point phone menus. They belong here because the menu lives on the entry point: the
            // resolver reads it, and the base feature has no entry points to read.
            .AddScoped<IIvrExecutionService, IvrExecutionService>()
            .AddScoped<IEntryPointFlowResolver, EntryPointFlowResolver>()
            .AddScoped<IInboundVoiceDigitsSink, InboundVoiceDigitsSink>()
            .AddScoped<IEntryPointResolver, EntryPointResolver>()
            .AddScoped<IPendingIncomingCallOfferService, PendingIncomingCallOfferService>()
            .AddScoped<QueuedVoiceWorkOfferScopeContext>()
            .AddScoped<IContactCenterEventHandler, OfferQueuedVoiceWorkOnAvailabilityHandler>()
            .AddScoped<ICatalogEntryHandler<ContactCenterEntryPoint>, ContactCenterEntryPointHandler>()
            .AddScoped<ICatalogEntryHandler<ContactCenterEntryPoint>, ContactCenterConfigurationCacheInvalidationHandler<ContactCenterEntryPoint>>()
            .AddIndexProvider<ContactCenterEntryPointIndexProvider>()
            .AddDataMigration<ContactCenterEntryPointIndexMigrations>();

        // Caller-based priority: the entry point says what the number is worth, the contributors notice what
        // this particular caller is worth, and the strongest of the two decides where they land in line.
        services.AddScoped<IInboundPriorityResolver, InboundPriorityResolver>();
        services.AddScoped<IInboundPriorityContributor, ReturningCallbackPriorityContributor>();
        services.AddScoped<IInboundPriorityContributor, RepeatCallerPriorityContributor>();

        // This feature is the one that queues inbound voice work, so it replaces the do-nothing default the base
        // feature registers for tenants without it.
        services.Replace(ServiceDescriptor.Scoped<IQueuedVoiceWorkOfferService, QueuedVoiceWorkOfferService>());

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
