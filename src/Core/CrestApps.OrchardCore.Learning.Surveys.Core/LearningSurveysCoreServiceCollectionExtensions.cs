using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Core.Services;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Learning surveys core.
/// </summary>
public static class LearningSurveysCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the survey course item kind (feedback surveys and choices), the response store and the response
    /// service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningSurveysCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SurveyResponseStore>();
        services.TryAddScoped<ISurveyContentSource, DefaultSurveyContentSource>();
        services.TryAddScoped<ISurveyResponseService, DefaultSurveyResponseService>();
        services.AddCourseItemKind<SurveyPart>(SurveysConstants.SurveyKind, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Survey", typeof(LearningSurveysCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Survey", typeof(LearningSurveysCoreServiceCollectionExtensions));
            kind.ContentType = SurveysConstants.ContentTypes.FeedbackSurvey;

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
