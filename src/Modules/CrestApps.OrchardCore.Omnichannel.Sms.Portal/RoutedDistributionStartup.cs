using CrestApps.OrchardCore.Omnichannel.Sms.Portal.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal;

/// <summary>
/// Registers the <b>SMS Portal Routed Distribution</b> feature: push assignment of a new department
/// conversation to a specific eligible agent, and the sweep that returns an unpicked routed conversation to its
/// shared pool. This is the only part of the workspace that needs Contact Center work distribution, so it is a
/// feature of its own. Without it the workspace still routes to personal numbers and to a queue's shared pool.
/// </summary>
[Feature(SmsPortalConstants.Feature.RoutedDistribution)]
public sealed class RoutedDistributionStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public RoutedDistributionStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Routed (push) SMS distribution tunables, overridable via the CrestApps:Sms:RoutedDistribution section.
        services.Configure<SmsRoutedDistributionOptions>(
            _shellConfiguration.GetSection("CrestApps:Sms:RoutedDistribution"));

        // The routed router joins the inbound chain at Order 250, before the shared-pool router, and declines when
        // no agent is eligible so the message still lands in the pool.
        services
            .AddScoped<ISmsInboundRouter, RoutedQueueRouter>()
            .Replace(ServiceDescriptor.Scoped<ISmsRoutingStrategy, LeastLoadedSmsRoutingStrategy>())
            .AddScoped<ISmsRoutedReassignmentService, SmsRoutedReassignmentService>();

        services.AddSingleton<IBackgroundTask, SmsRoutedReassignmentBackgroundTask>();
    }
}
