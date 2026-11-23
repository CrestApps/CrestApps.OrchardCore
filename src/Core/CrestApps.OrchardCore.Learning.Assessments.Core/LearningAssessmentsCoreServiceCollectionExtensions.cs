using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the core services of Learning Assessments.
/// </summary>
public static class LearningAssessmentsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the quiz course item kind, its result provider, the grade scale store and the grading queue.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLearningAssessmentsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<GradeScaleStore>();
        services.TryAddScoped<GradingScope>();
        services.TryAddScoped<GradingQueue>();
        services.AddGradingQueueSource<QuizResponseGradingQueueSource>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILearningSurveyItemResultProvider, QuizResultProvider>());
        services.AddSurveyAccessProvider<AssessmentsSurveyAccessProvider>();
        services.AddSurveyScoreChangeCheck<AssessmentsScoreChangeCheck>();
        services.AddResumableStep<LegacyQuizUpgradeStep>();

        services.AddCourseItemKind(AssessmentsConstants.QuizKind, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Quiz", typeof(LearningAssessmentsCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Quiz", typeof(LearningAssessmentsCoreServiceCollectionExtensions));
            kind.PartName = "QuizPart";
            kind.ContentType = AssessmentsConstants.ContentTypes.Quiz;
            kind.CompletionModes.Add(CrestApps.OrchardCore.Learning.Models.CompletionModes.Passed);
            kind.CompletionModes.Add(CrestApps.OrchardCore.Learning.Models.CompletionModes.Submitted);
            kind.CompletionModes.Add(CrestApps.OrchardCore.Learning.Models.CompletionModes.Graded);
            kind.ProducesGrade = true;
            kind.DecidesPass = true;
            kind.Icon = "fa-solid fa-circle-question";
        });

        return services;
    }
}
