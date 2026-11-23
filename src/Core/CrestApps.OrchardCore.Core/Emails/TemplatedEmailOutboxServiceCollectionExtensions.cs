using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Emails;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using YesSql.Indexes;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the durable templated email outbox.
/// </summary>
public static class TemplatedEmailOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITemplatedEmailOutbox"/> and <see cref="ITemplatedEmailOutboxStore"/>, the outbox's
    /// collection, index provider and migration, the background task that tries again the emails the email service
    /// didn't take, and the retention of sent and given-up emails. Several features may call it: the outbox is
    /// registered once, and its migration belongs to the first feature that registers it.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTemplatedEmailOutbox(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTemplatedEmails();

        if (services.Any(descriptor => descriptor.ServiceType == typeof(TemplatedEmailOutbox)))
        {
            return services;
        }

        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TemplatedEmailOutboxStorage.CollectionName));

        services.TryAddScoped<TemplatedEmailOutbox>();
        services.TryAddScoped<ITemplatedEmailOutbox>(serviceProvider => serviceProvider.GetRequiredService<TemplatedEmailOutbox>());
        services.TryAddScoped<ITemplatedEmailOutboxStore, TemplatedEmailOutboxStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IIndexProvider, TemplatedEmailOutboxIndexProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDataMigration, TemplatedEmailOutboxMigrations>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, TemplatedEmailOutboxBackgroundTask>());
        services.AddRetentionPolicy<TemplatedEmailOutboxRetentionPolicy>();

        return services;
    }

    /// <summary>
    /// Adds an observer of the attempts of the outbox's emails, once however many times it is added.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTemplatedEmailOutboxObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ITemplatedEmailOutboxObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITemplatedEmailOutboxObserver, TObserver>());

        return services;
    }
}
