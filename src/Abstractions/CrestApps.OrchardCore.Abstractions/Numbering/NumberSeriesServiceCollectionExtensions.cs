using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Numbering;

/// <summary>
/// Registers number series.
/// </summary>
public static class NumberSeriesServiceCollectionExtensions
{
    /// <summary>
    /// Registers a number series, so an <see cref="INumberSeries"/> issues its numbers with this format until the site
    /// chooses another one, and a settings page can list it. Registering a name again replaces it.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The name of the series, such as <c>Financial.Invoice</c>.</param>
    /// <param name="title">The title shown for the series.</param>
    /// <param name="defaultFormat">How the numbers are written when the site has not chosen a format.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddNumberSeries(this IServiceCollection services, string name, LocalizationSource title, NumberSeriesFormat defaultFormat)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(defaultFormat);

        services.Configure<NumberSeriesOptions>(options => options.Series[name] = new NumberSeries
        {
            Name = name,
            Title = title,
            DefaultFormat = defaultFormat,
        });

        return services;
    }
}
