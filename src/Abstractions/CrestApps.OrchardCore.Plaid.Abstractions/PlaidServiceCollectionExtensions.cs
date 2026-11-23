using CrestApps.OrchardCore.Plaid.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Plaid;

/// <summary>
/// Registers what other modules and hosts add to Plaid.
/// </summary>
public static class PlaidServiceCollectionExtensions
{
    /// <summary>
    /// Registers a handler that every verified Plaid webhook is handed to.
    /// </summary>
    /// <typeparam name="THandler">The handler.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPlaidWebhookHandler<THandler>(this IServiceCollection services)
        where THandler : class, IPlaidWebhookHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPlaidWebhookHandler, THandler>());

        return services;
    }

    /// <summary>
    /// Replaces where the tenant's Plaid credentials come from, such as keys the host holds for every tenant, instead of
    /// the Plaid settings of each site. It wins whether it is registered before or after the Plaid feature's own
    /// settings-based provider, which is only added when no other provider is registered.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPlaidCredentialsProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IPlaidCredentialsProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IPlaidCredentialsProvider>();
        services.AddScoped<IPlaidCredentialsProvider, TProvider>();

        return services;
    }
}
