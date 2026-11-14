using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Payroll.Core;

/// <summary>
/// Registers the services of the Payroll feature.
/// </summary>
public static class PayrollCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers employers, the payroll scope and the employee directory, on top of Pay Setup.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayrollCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPaySetupCore();
        services.AddCatalogs().AddCatalogManagers();
        services.TryAddScoped<EmployerStore>();
        services.TryAddScoped<IEmployerManager, EmployerManager>();
        services.TryAddScoped<ICatalogManager<Employer>>(serviceProvider => serviceProvider.GetRequiredService<IEmployerManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Employer>, EmployerHandler>());
        services.TryAddScoped<IPayrollScopeService, PayrollScopeService>();
        services.TryAddScoped<IPayrollEmployeeDirectory, EmptyPayrollEmployeeDirectory>();

        return services;
    }
}
