using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers inbound voice entry-point administration, qualification, and queue ingress.
/// </summary>
[Feature(ContactCenterConstants.Feature.EntryPoints)]
public sealed class EntryPointsStartup : StartupBase
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

    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddVoiceIngressEndpoint();
    }
}
