using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.YesSql.Core.Numbering;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the shared <see cref="INumberSequence"/>.
/// </summary>
public static class NumberSequenceServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="INumberSequence"/> that keeps each series in a YesSql document. Call it from every feature that
    /// issues numbers; it adds the service once.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddNumberSequence(this IServiceCollection services)
    {
        services.TryAddScoped<INumberSequence, DocumentNumberSequence>();

        return services;
    }
}
