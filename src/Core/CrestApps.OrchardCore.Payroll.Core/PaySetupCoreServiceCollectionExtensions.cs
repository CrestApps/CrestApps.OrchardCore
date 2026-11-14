using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Payroll.Core.Handlers;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Payroll.Core;

/// <summary>
/// Registers Pay Setup.
/// </summary>
public static class PaySetupCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the stores, managers and rules of pay schedules and pay codes.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPaySetupCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCatalogs().AddCatalogManagers();
        services.TryAddScoped<PayScheduleStore>();
        services.TryAddScoped<PayCodeStore>();
        services.TryAddScoped<IPayCodeLookup>(serviceProvider => new PayCodeStoreLookup(serviceProvider.GetRequiredService<PayCodeStore>()));
        services.TryAddScoped<IPayScheduleManager, PayScheduleManager>();
        services.TryAddScoped<IPayCodeManager, PayCodeManager>();
        services.TryAddScoped<ICatalogManager<PaySchedule>>(serviceProvider => serviceProvider.GetRequiredService<IPayScheduleManager>());
        services.TryAddScoped<ICatalogManager<PayCode>>(serviceProvider => serviceProvider.GetRequiredService<IPayCodeManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PaySchedule>, PayScheduleHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PayCode>, PayCodeHandler>());
        services.TryAddScoped<PayCodeSeeder>();
        services.TryAddScoped<IPayInputService, DefaultPayInputService>();

        return services;
    }

    private sealed class PayCodeStoreLookup : IPayCodeLookup
    {
        private readonly PayCodeStore _store;

        public PayCodeStoreLookup(PayCodeStore store)
        {
            _store = store;
        }

        public Task<PayCode> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
            => _store.FindByCodeAsync(code, cancellationToken);
    }
}
