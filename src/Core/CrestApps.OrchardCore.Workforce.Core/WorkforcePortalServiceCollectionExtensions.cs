using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Workforce.Core;

/// <summary>
/// Registers the My Work portal's services.
/// </summary>
public static class WorkforcePortalServiceCollectionExtensions
{
    /// <summary>
    /// Registers the notifier with the inbox and web push channels, the notices on publishes and changes, web push
    /// and the worker's calendar feed. Call it with
    /// <see cref="WorkforceCoreServiceCollectionExtensions.AddWorkforceCore"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWorkforcePortalCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<WorkforceInboxStore>();
        services.TryAddScoped<WebPushSubscriptionStore>();
        services.TryAddScoped<InboxNotificationChannel>();
        services.TryAddScoped<IWorkforceInbox>(provider => provider.GetRequiredService<InboxNotificationChannel>());
        services.TryAddScoped<DefaultWebPushService>();
        services.TryAddScoped<IWebPushService>(provider => provider.GetRequiredService<DefaultWebPushService>());
        services.TryAddScoped<IWorkerFeedService, DefaultWorkerFeedService>();
        services.AddHttpClient(WorkforceConstants.WebPushHttpClient);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkforceNotificationChannel, InboxNotificationChannel>(provider => provider.GetRequiredService<InboxNotificationChannel>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkforceNotificationChannel, WebPushNotificationChannel>());
        services.Replace(ServiceDescriptor.Scoped<IWorkforceNotifier, DefaultWorkforceNotifier>());
        services.TryAddScoped<PortalNotices>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISchedulePublishHandler, PortalNotices>(provider => provider.GetRequiredService<PortalNotices>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IScheduleChangeHandler, PortalNotices>(provider => provider.GetRequiredService<PortalNotices>()));

        return services;
    }
}
