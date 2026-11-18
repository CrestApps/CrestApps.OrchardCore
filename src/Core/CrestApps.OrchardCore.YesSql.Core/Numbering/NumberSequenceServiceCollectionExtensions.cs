using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.YesSql.Core.Numbering;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the shared <see cref="INumberSequence"/> and <see cref="INumberSeries"/>.
/// </summary>
public static class NumberSequenceServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="INumberSequence"/> that keeps each series in a YesSql document, and the
    /// <see cref="INumberSeries"/> that writes its numbers in the format of a registered series. Call it from every
    /// feature that issues numbers; it adds the services once.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddNumberSequence(this IServiceCollection services)
    {
        services.TryAddScoped<INumberSequence, DocumentNumberSequence>();
        services.TryAddScoped<INumberSeries, NumberSeriesService>();

        return services;
    }
}
