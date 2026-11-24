using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers what modules add to Tax Forms.
/// </summary>
public static class TaxFormsServiceCollectionExtensions
{
    /// <summary>
    /// Adds a tax form source: a module that prepares tax forms from its own records, such as Payroll.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The stored name; it never changes.</param>
    /// <param name="displayName">What people see.</param>
    /// <param name="description">What it prepares forms from.</param>
    /// <param name="reportedAmountsLabel">What the records that report amounts to a field are called, such as pay codes; or <see langword="null"/>.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTaxFormSource(this IServiceCollection services, string name, LocalizationSource displayName, LocalizationSource description, LocalizationSource reportedAmountsLabel = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return services.Configure<TaxFormSourceOptions>(options =>
        {
            options.Sources.RemoveAll(source => string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase));
            options.Sources.Add(new TaxFormSource(name, displayName, description, reportedAmountsLabel));
        });
    }

    /// <summary>
    /// Adds what tells whether a tax form is used, such as by a module's filings.
    /// </summary>
    /// <typeparam name="TUsage">The usage.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTaxFormUsage<TUsage>(this IServiceCollection services)
        where TUsage : class, ITaxFormUsage
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITaxFormUsage, TUsage>());

        return services;
    }
}
