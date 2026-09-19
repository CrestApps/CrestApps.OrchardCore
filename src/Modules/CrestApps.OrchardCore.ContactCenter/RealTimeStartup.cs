using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using CrestApps.OrchardCore.ContactCenter.Core.Hubs;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the shared SignalR hub and event projection that broadcasts presence, offer, and queue
/// updates to optional real-time user experiences.
/// </summary>
[Feature(ContactCenterConstants.Feature.RealTime)]
public sealed class RealTimeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterCapability(ContactCenterConstants.Feature.RealTime, ContactCenterCapabilities.RealTime);

        // The hub asks for its own operations, so this feature answers them rather than relying on
        // another enabled feature having registered the handler first.
        services.AddContactCenterOperationAuthorization();

        services
            .AddScoped<ContactCenterHubScopeContext>()
            .AddScoped<ContactCenterRealTimeEventScopeContext>()
            .AddScoped<IContactCenterRealTimeNotifier, ContactCenterRealTimeNotifier>()
            .AddScoped<IContactCenterEventHandler, ContactCenterRealTimeEventHandler>()
            .AddSingleton<ContactCenterHubConnectionRegistry>()
            .AddScoped<ContactCenterRealTimeLifecycleParticipant>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                serviceProvider.GetRequiredService<ContactCenterRealTimeLifecycleParticipant>());

        services.AddResourceConfiguration<ContactCenterRealTimeResourceConfiguration>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<ContactCenterHub>(SignalRHubRoutes.GetHubPath<ContactCenterHub>());
    }
}
