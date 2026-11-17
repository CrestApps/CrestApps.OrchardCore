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
    /// Adds a kind of income tax rule to the list "Add income tax rule" shows. Adding a name again replaces it. Its
    /// fields come from a display driver of <see cref="Taxation.Models.IncomeTaxRule"/> that handles only rules whose
    /// <c>Source</c> is <paramref name="name"/>; a source whose <paramref name="method"/> is a withholding method gets
    /// that method's tables and amounts from Taxation's own driver.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The stored name; it never changes.</param>
    /// <param name="displayName">What people see.</param>
    /// <param name="description">What the kind of rule is for, with examples.</param>
    /// <param name="method">The method its rules always use, or <see langword="null"/> when the editor chooses it.</param>
    /// <param name="position">Where it is listed; lower first.</param>
    /// <param name="fields">The amount fields its rules hold; <see langword="null"/> for a withholding method's
    /// (<see cref="CrestApps.OrchardCore.Taxation.Models.IncomeTaxRuleSources.WithholdingFields"/>).</param>
    /// <param name="usesWithholdingEditor">Whether Taxation's withholding editor edits it; <see langword="false"/> when the
    /// pack's own driver does.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddIncomeTaxRuleSource(
        this IServiceCollection services,
        string name,
        LocalizationSource displayName,
        LocalizationSource description,
        string method = null,
        int position = 100,
        IReadOnlyList<string> fields = null,
        bool usesWithholdingEditor = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return services.Configure<IncomeTaxRuleSourceOptions>(options => options.Sources[name] = new IncomeTaxRuleSource
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            Method = method,
            Position = position,
            Fields = fields ?? CrestApps.OrchardCore.Taxation.Models.IncomeTaxRuleSources.WithholdingFields,
            UsesWithholdingEditor = usesWithholdingEditor,
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
