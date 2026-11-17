using CrestApps.OrchardCore.Fulfillment.Core.Handlers;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the fulfillment engine.
/// </summary>
public static class FulfillmentServiceCollectionExtensions
{
    /// <summary>
    /// Registers the request store, the order checks, the line assigner, the sender and the paid-order handler. The
    /// module registers the index provider and the migration; Shipping registers the shipment services this builds on.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddFulfillmentCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<FulfillmentRequestStore>();
        services.TryAddScoped<FulfillmentOrderBuilder>();
        services.TryAddScoped<FulfillmentLineAssigner>();
        services.TryAddScoped<FulfillmentSender>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrderEventHandler, FulfillmentOrderEventHandler>());

        return services;
    }
}
