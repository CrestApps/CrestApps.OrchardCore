using CrestApps.Core;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Taxation.Core;

/// <summary>
/// Provides extension methods to register the taxation framework services.
/// </summary>
public static class TaxationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the provider-agnostic taxation engine, its default implementations, calculation methods,
    /// sourcing strategies, catalog stores, resolvers, and providers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, to allow chaining.</returns>
    public static IServiceCollection AddTaxationCore(this IServiceCollection services)
    {
        services
            .AddCatalogs()
            .AddCatalogManagers();

        services.TryAddScoped<ITaxJurisdictionStore, TaxJurisdictionStore>();
        services.TryAddScoped<ITaxCategoryStore, TaxCategoryStore>();
        services.TryAddScoped<ITaxRuleStore, TaxRuleStore>();
        services.TryAddScoped<ITaxTableStore, TaxTableStore>();
        services.TryAddScoped<IExemptionCertificateStore, ExemptionCertificateStore>();
        services.TryAddScoped<IMerchantTaxRegistrationStore, MerchantTaxRegistrationStore>();

        services.TryAddScoped<ITaxService, TaxService>();
        services.TryAddScoped<ITaxableBaseCalculator, DefaultTaxableBaseCalculator>();
        services.TryAddScoped<ITaxRoundingStrategy, DefaultTaxRoundingStrategy>();
        services.TryAddScoped<ITaxCalculationMethodProvider, DefaultTaxCalculationMethodProvider>();
        services.TryAddScoped<ITaxSourcingStrategyProvider, DefaultTaxSourcingStrategyProvider>();
        services.TryAddScoped<ITaxRuleProvider, CatalogTaxRuleProvider>();
        services.TryAddScoped<ITaxJurisdictionResolver, CatalogTaxJurisdictionResolver>();
        services.TryAddScoped<ITaxExemptionResolver, CatalogTaxExemptionResolver>();
        services.TryAddScoped<IMerchantTaxRegistrationProvider, CatalogMerchantTaxRegistrationProvider>();
        services.TryAddScoped<ITaxableItemResolver, DefaultTaxableItemResolver>();
        services.TryAddScoped<ITaxSnapshotFactory, DefaultTaxSnapshotFactory>();
        services.TryAddScoped<ITaxRefundCalculator, DefaultTaxRefundCalculator>();

        services.AddTaxCalculationMethod<PercentageTaxCalculationMethod>();
        services.AddTaxCalculationMethod<FixedAmountTaxCalculationMethod>();
        services.AddTaxCalculationMethod<PerUnitTaxCalculationMethod>();
        services.AddTaxCalculationMethod<PerWeightTaxCalculationMethod>();
        services.AddTaxCalculationMethod<PerVolumeTaxCalculationMethod>();
        services.AddTaxCalculationMethod<TaxTableTaxCalculationMethod>();
        services.AddTaxCalculationMethod<ProgressiveTaxCalculationMethod>();
        services.AddTaxCalculationMethod<ThresholdTaxCalculationMethod>();

        services.AddTaxSourcingStrategy<OriginTaxSourcingStrategy>();
        services.AddTaxSourcingStrategy<DestinationTaxSourcingStrategy>();
        services.AddTaxSourcingStrategy<CustomerResidenceTaxSourcingStrategy>();
        services.AddTaxSourcingStrategy<CustomerBusinessTaxSourcingStrategy>();
        services.AddTaxSourcingStrategy<ServiceLocationTaxSourcingStrategy>();
        services.AddTaxSourcingStrategy<EventLocationTaxSourcingStrategy>();

        return services;
    }

    /// <summary>
    /// Registers a tax calculation method so that it can be resolved by name.
    /// </summary>
    /// <typeparam name="TMethod">The calculation method type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, to allow chaining.</returns>
    public static IServiceCollection AddTaxCalculationMethod<TMethod>(this IServiceCollection services)
        where TMethod : class, ITaxCalculationMethod
    {
        services.AddScoped<ITaxCalculationMethod, TMethod>();

        return services;
    }

    /// <summary>
    /// Registers a tax sourcing strategy so that it can be resolved by name.
    /// </summary>
    /// <typeparam name="TStrategy">The sourcing strategy type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, to allow chaining.</returns>
    public static IServiceCollection AddTaxSourcingStrategy<TStrategy>(this IServiceCollection services)
        where TStrategy : class, ITaxSourcingStrategy
    {
        services.AddScoped<ITaxSourcingStrategy, TStrategy>();

        return services;
    }

    /// <summary>
    /// Registers an external tax determination provider that can short-circuit the built-in engine.
    /// </summary>
    /// <typeparam name="TProvider">The determination provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, to allow chaining.</returns>
    public static IServiceCollection AddTaxDeterminationProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ITaxDeterminationProvider
    {
        services.AddScoped<ITaxDeterminationProvider, TProvider>();

        return services;
    }

    /// <summary>
    /// Registers a taxable item provider that converts an arbitrary object into a taxable item.
    /// </summary>
    /// <typeparam name="TProvider">The taxable item provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, to allow chaining.</returns>
    public static IServiceCollection AddTaxableItemProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ITaxableItemProvider
    {
        services.AddScoped<ITaxableItemProvider, TProvider>();

        return services;
    }
}
