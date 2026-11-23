using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Surveys.Core;

/// <summary>
/// Registers the results services of Surveys.
/// </summary>
public static class SurveysResultsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the answer aggregates, the results queries, the anonymous small-group rule and the definition reader of the answer index.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysResults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMemoryCache();
        services.TryAddScoped<ISurveyAnswerAggregates, SurveyAnswerAggregates>();
        services.TryAddScoped<SurveyResultsQueries>();
        services.TryAddScoped<SurveyAnonymousGroups>();
        services.TryAddScoped<SurveyVersionDefinitionReader>();

        return services;
    }
}
