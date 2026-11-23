using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Core;
using CrestApps.OrchardCore.Learning.Surveys.Core.Upgrades;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the core services of Learning Surveys.
/// </summary>
public static class LearningSurveysCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the survey course item kind and the result provider of course surveys.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningSurveysCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILearningSurveyItemResultProvider, CourseSurveyResultProvider>());
        services.TryAddScoped<AnonymousCourseSurveyCompletions>();
        services.AddResumableStep<LegacyCourseSurveyUpgradeStep>();
        services.AddCourseItemKind<CourseSurveyPart>(LearningSurveysConstants.SurveyKind, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Survey", typeof(LearningSurveysCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Survey", typeof(LearningSurveysCoreServiceCollectionExtensions));
            kind.ContentType = LearningSurveysConstants.ContentTypes.FeedbackSurvey;

            // Answering first, so it is the default; a survey gives no grade.
            kind.CompletionModes.Add(CompletionModes.Submitted);
            kind.CompletionModes.Add(CompletionModes.Viewed);
            kind.CompletionModes.Add(CompletionModes.MarkedDone);
            kind.ProducesGrade = false;
            kind.Icon = "fa-solid fa-square-poll-vertical";
        });

        return services;
    }
}
