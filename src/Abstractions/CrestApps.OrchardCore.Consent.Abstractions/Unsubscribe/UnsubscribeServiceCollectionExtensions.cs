using CrestApps.OrchardCore.Consent.Unsubscribe;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the unsubscribe scopes modules add to the e-mail preferences page.
/// </summary>
public static class UnsubscribeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the handler of one unsubscribe scope kind.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddUnsubscribeScope<THandler>(this IServiceCollection services)
        where THandler : class, IUnsubscribeScopeHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnsubscribeScopeHandler, THandler>());

        return services;
    }
}
