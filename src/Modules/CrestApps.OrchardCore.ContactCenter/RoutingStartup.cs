using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers policy-based routing strategies and activity assignment orchestration.
/// </summary>
[Feature(ContactCenterConstants.Feature.Routing)]
public sealed class RoutingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IActivityRoutingService, ActivityRoutingService>()
            .AddScoped<IActivityRoutingStrategy, RequiredSkillsRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, CapacityRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, StickyAgentRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, LongestIdleRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, RoundRobinRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, LeastBusyRoutingStrategy>()
            .AddScoped<IActivityAssignmentService, ActivityAssignmentService>();

        services.AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
            new ContactCenterFeatureWorkLifecycleParticipant(
                ContactCenterConstants.Feature.Routing,
                serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

        services.AddSingleton<IBackgroundTask, ReservationExpiryBackgroundTask>();
    }
}
