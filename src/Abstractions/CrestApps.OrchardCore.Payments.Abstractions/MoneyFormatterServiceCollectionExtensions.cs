using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the shared <see cref="IMoneyFormatter"/>.
/// </summary>
public static class MoneyFormatterServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default <see cref="IMoneyFormatter"/> unless one is registered. Call it from every feature that shows
    /// money through the formatter; it adds the service once.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddMoneyFormatter(this IServiceCollection services)
    {
        services.TryAddSingleton<IMoneyFormatter, DefaultMoneyFormatter>();

        return services;
    }
}
