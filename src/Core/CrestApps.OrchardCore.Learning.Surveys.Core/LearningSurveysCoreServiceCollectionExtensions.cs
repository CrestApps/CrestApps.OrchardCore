using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the core services of Learning Surveys.
/// </summary>
public static class LearningSurveysCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the result provider of course surveys.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningSurveysCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILearningSurveyItemResultProvider, CourseSurveyResultProvider>());

        return services;
    }
}
