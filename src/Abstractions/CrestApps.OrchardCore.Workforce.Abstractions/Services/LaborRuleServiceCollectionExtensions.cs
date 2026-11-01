using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Registers labor rules in the catalog.
/// </summary>
public static class LaborRuleServiceCollectionExtensions
{
    /// <summary>
    /// Adds a labor rule to the catalog.
    /// </summary>
    /// <typeparam name="TRule">The rule.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLaborRule<TRule>(this IServiceCollection services)
        where TRule : class, ILaborRule
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILaborRule, TRule>());

        return services;
    }
}
