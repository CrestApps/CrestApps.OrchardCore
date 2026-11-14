using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

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
        services.AddSensitiveValueProtector();
        services.TryAddScoped<PayProfileStore>();
        services.TryAddScoped<PayItemStore>();
        services.TryAddScoped<IPayProfileManager, PayProfileManager>();
        services.TryAddScoped<IPayItemManager, PayItemManager>();
        services.TryAddScoped<ICatalogManager<EmployeePayProfile>>(serviceProvider => serviceProvider.GetRequiredService<IPayProfileManager>());
        services.TryAddScoped<ICatalogManager<EmployeePayItem>>(serviceProvider => serviceProvider.GetRequiredService<IPayItemManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<EmployeePayProfile>, PayProfileHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<EmployeePayItem>, PayItemHandler>());
        services.TryAddScoped<PayrollTaxIdHasher>();
        services.TryAddScoped<DefaultPayProfileService>();
        services.TryAddScoped<IPayProfileService>(serviceProvider => serviceProvider.GetRequiredService<DefaultPayProfileService>());
        services.TryAddScoped<IPayrollEmployeeDirectory, PayProfileEmployeeDirectory>();
        services.TryAddScoped<PayProfileUsage>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEmployerUsage, PayProfileUsage>(serviceProvider => serviceProvider.GetRequiredService<PayProfileUsage>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPaySetupUsage, PayProfileUsage>(serviceProvider => serviceProvider.GetRequiredService<PayProfileUsage>()));

        services.TryAddScoped<PayrollPayCodeSeeder>();
        services.TryAddScoped<PayStatementStore>();
        services.TryAddScoped<IPayAccumulatorService, PayAccumulatorService>();
        services.TryAddScoped<IPayCalculator, PayCalculator>();
        services.TryAddScoped<PayCalculationContextFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, EarningsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, RegularRatePayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, PreTaxDeductionsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, TaxesPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, PostTaxDeductionsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, DisposableEarningsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, NetCheckPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, ContributionsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, DistributionPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPaySetupUsage, PayCodeBaseUsage>());

        // Any jurisdiction gets the usual withholding fields until a country pack gives its own form.
        services.AddElectionFieldSet(GenericElectionFields.Create());

        // The tax categories every country has; a country pack adds its own.
        services.AddPayrollTaxCategory(PayrollTaxCategories.IncomeTax, LocalizationSource.Create("Income tax", typeof(PayrollCoreServiceCollectionExtensions)));
        services.AddPayrollTaxCategory(PayrollTaxCategories.SocialInsurance, LocalizationSource.Create("Social insurance", typeof(PayrollCoreServiceCollectionExtensions)));
        services.AddPayrollTaxCategory(PayrollTaxCategories.Unemployment, LocalizationSource.Create("Unemployment insurance", typeof(PayrollCoreServiceCollectionExtensions)));

        return services;
    }
}
