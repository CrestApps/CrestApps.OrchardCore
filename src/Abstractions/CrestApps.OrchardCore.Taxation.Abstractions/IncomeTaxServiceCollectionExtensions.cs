using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers what country packs add to Income Tax: tax categories, election forms and withholding methods.
/// </summary>
public static class IncomeTaxServiceCollectionExtensions
{
    /// <summary>
    /// Adds a tax category income is sorted into. Adding a name again replaces its display name.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The stored name; it never changes.</param>
    /// <param name="displayName">What people see.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddIncomeTaxCategory(this IServiceCollection services, string name, LocalizationSource displayName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return services.Configure<IncomeTaxCategoryOptions>(options =>
        {
            options.Categories.RemoveAll(category => category.Name == name);
            options.Categories.Add(new IncomeTaxCategory(name, displayName));
        });
    }

    /// <summary>
    /// Adds the fields of a tax election for jurisdictions, such as a country pack's withholding form. A set for the same
    /// jurisdictions replaces the earlier one.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="fieldSet">The field set.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddElectionFieldSet(this IServiceCollection services, ElectionFieldSet fieldSet)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(fieldSet);

        return services.Configure<IncomeTaxElectionOptions>(options =>
        {
            foreach (var same in options.FieldSets.Where(set => string.Equals(set.Jurisdiction, fieldSet.Jurisdiction, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                options.FieldSets.Remove(same);
            }

            options.FieldSets.Add(fieldSet);
        });
    }

    /// <summary>
    /// Adds a withholding method that income tax rules can name, such as a country's withholding formulas.
    /// </summary>
    /// <typeparam name="TMethod">The method.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddIncomeTaxWithholdingMethod<TMethod>(this IServiceCollection services)
        where TMethod : class, IIncomeTaxWithholdingMethod
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIncomeTaxWithholdingMethod, TMethod>());

        return services;
    }
}
