using CrestApps.OrchardCore.Payroll.Services;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers tax categories and form boxes for pay codes.
/// </summary>
public static class PayrollTaxCategoryServiceCollectionExtensions
{
    /// <summary>
    /// Adds a tax category the Payroll card of a pay code sets a treatment for. Adding a name again replaces its
    /// display name.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The stored name; it never changes.</param>
    /// <param name="displayName">What people see.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayrollTaxCategory(this IServiceCollection services, string name, LocalizationSource displayName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return services.Configure<PayrollTaxCategoryOptions>(options =>
        {
            options.Categories.RemoveAll(category => category.Name == name);
            options.Categories.Add(new PayrollTaxCategory(name, displayName));
        });
    }

    /// <summary>
    /// Adds a box of a tax form that a pay code's amounts can go to.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The stored name; it never changes.</param>
    /// <param name="displayName">What people see.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayrollFormBox(this IServiceCollection services, string name, LocalizationSource displayName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return services.Configure<PayrollFormBoxOptions>(options =>
        {
            options.Boxes.RemoveAll(box => box.Name == name);
            options.Boxes.Add(new PayrollFormBox(name, displayName));
        });
    }
}
