using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Surveys.Core;

/// <summary>
/// Registers scoring, review, grading and accommodations.
/// </summary>
public static class SurveysScoringServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scoring engine, the review policy, the grading service and queue and the accommodation service.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysScoring(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ISurveyScoreFormulaEvaluator, NullSurveyScoreFormulaEvaluator>();
        services.TryAddScoped<ISurveyHiddenQuestionResolver, NullSurveyHiddenQuestionResolver>();
        services.TryAddScoped<ISurveyResponseCommitter, YesSqlSurveyResponseCommitter>();
        services.TryAddSingleton<ISurveyScoringEventDispatcher, DeferredSurveyScoringEventDispatcher>();
        services.TryAddSingleton<ISurveyReviewPolicy, SurveyReviewPolicy>();
        services.TryAddScoped<ISurveyScoringEngine, SurveyScoringEngine>();
        services.TryAddScoped<ISurveyGradingService, SurveyGradingService>();
        services.TryAddScoped<ISurveyGradingQueue, SurveyGradingQueue>();
        services.TryAddScoped<ISurveyOverrideService, SurveyOverrideService>();

        return services;
    }
}
