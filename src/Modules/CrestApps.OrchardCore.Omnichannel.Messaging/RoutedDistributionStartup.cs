using CrestApps.OrchardCore.Omnichannel.Messaging.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Registers the <b>Omnichannel Messaging Routed Distribution</b> feature: push assignment of a new department
/// conversation to a specific eligible agent, and the sweep that returns an unpicked routed conversation to its
/// shared pool. This is the only part of the workspace that needs Contact Center work distribution, so it is a
/// feature of its own. Without it the workspace still routes to personal numbers and to a queue's shared pool.
/// </summary>
[Feature(MessagingConstants.Feature.RoutedDistribution)]
public sealed class RoutedDistributionStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public RoutedDistributionStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Routed (push) distribution tunables, overridable via the CrestApps:Omnichannel:Messaging:RoutedDistribution section.
        services.Configure<MessagingRoutedDistributionOptions>(
            _shellConfiguration.GetSection("CrestApps:Omnichannel:Messaging:RoutedDistribution"));

        // The routed router joins the inbound chain at Order 250, before the shared-pool router, and declines when
        // no agent is eligible so the message still lands in the pool.
        services
            .AddScoped<IMessagingInboundRouter, RoutedQueueRouter>()
            .Replace(ServiceDescriptor.Scoped<IMessagingRoutingStrategy, LeastLoadedMessagingRoutingStrategy>())
            .AddScoped<IMessagingRoutedReassignmentService, MessagingRoutedReassignmentService>();

        services.AddSingleton<IBackgroundTask, MessagingRoutedReassignmentBackgroundTask>();
    }
}
