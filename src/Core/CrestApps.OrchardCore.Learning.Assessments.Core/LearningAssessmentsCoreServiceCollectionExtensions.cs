using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the assessments core.
/// </summary>
public static class LearningAssessmentsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the stores, the scorers of the nine question types, the attempt service and the deadline queue.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningAssessmentsCore(this IServiceCollection services)
    {
        services.TryAddScoped<AssessmentAttemptStore>();
        services.TryAddScoped<AssessmentOverrideStore>();
        services.TryAddScoped<GradeScaleStore>();
        services.TryAddScoped<IAssessmentContentSource, DefaultAssessmentContentSource>();
        services.TryAddScoped<AttemptReviewPolicy>();
        services.AddAttemptObserver<AssessmentsTelemetryObserver>();
        services.TryAddScoped<IAssessmentAttemptService, DefaultAssessmentAttemptService>();
        services.TryAddScoped(sp => new Lazy<IAuthorizationService>(sp.GetRequiredService<IAuthorizationService>));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, QuestionContentAccessHandler>());
        services.AddLeasedWorkQueue<AttemptDeadlineWork>(options =>
        {
            options.CollectionName = AttemptDeadlineWork.CollectionName;
            options.RemoveCompletedItems = true;
        });

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
            kind.PlayerShape = "QuizPart_Player";
            kind.Icon = "fa-solid fa-circle-question";
        });

        AddQuestionType<SingleChoiceScorer>(services, AssessmentsConstants.QuestionTypes.SingleChoice, AssessmentsConstants.ContentTypes.SingleChoiceQuestion, "Single choice");
        AddQuestionType<MultipleChoiceScorer>(services, AssessmentsConstants.QuestionTypes.MultipleChoice, AssessmentsConstants.ContentTypes.MultipleChoiceQuestion, "Multiple choice");
        AddQuestionType<TrueFalseScorer>(services, AssessmentsConstants.QuestionTypes.TrueFalse, AssessmentsConstants.ContentTypes.TrueFalseQuestion, "True or false");
        AddQuestionType<ShortTextScorer>(services, AssessmentsConstants.QuestionTypes.ShortText, AssessmentsConstants.ContentTypes.ShortTextQuestion, "Short text");
        AddQuestionType<NumericScorer>(services, AssessmentsConstants.QuestionTypes.Numeric, AssessmentsConstants.ContentTypes.NumericQuestion, "Numeric");
        AddQuestionType<MatchingScorer>(services, AssessmentsConstants.QuestionTypes.Matching, AssessmentsConstants.ContentTypes.MatchingQuestion, "Matching");
        AddQuestionType<OrderingScorer>(services, AssessmentsConstants.QuestionTypes.Ordering, AssessmentsConstants.ContentTypes.OrderingQuestion, "Ordering");
        AddQuestionType<FillInBlanksScorer>(services, AssessmentsConstants.QuestionTypes.FillInBlanks, AssessmentsConstants.ContentTypes.FillInBlanksQuestion, "Fill in the blanks");
        AddQuestionType<EssayScorer>(services, AssessmentsConstants.QuestionTypes.Essay, AssessmentsConstants.ContentTypes.EssayQuestion, "Essay", manuallyGraded: true);

        return services;
    }

    private static void AddQuestionType<TScorer>(IServiceCollection services, string name, string contentType, string displayName, bool manuallyGraded = false)
        where TScorer : class, IQuestionScorer
        => services.AddQuestionType<TScorer>(name, type =>
        {
            type.ContentType = contentType;
            type.DisplayName = LocalizationSource.Create(displayName, typeof(LearningAssessmentsCoreServiceCollectionExtensions));
            type.ManuallyGraded = manuallyGraded;
        });
}
