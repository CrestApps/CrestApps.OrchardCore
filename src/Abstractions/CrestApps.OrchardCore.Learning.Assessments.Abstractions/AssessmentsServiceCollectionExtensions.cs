using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers extensions of Learning Assessments.
/// </summary>
public static class AssessmentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a source of work waiting for a grader on the Learning grading page.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddGradingQueueSource<TSource>(this IServiceCollection services)
        where TSource : class, IGradingQueueSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGradingQueueSource, TSource>());

        return services;
    }
}
