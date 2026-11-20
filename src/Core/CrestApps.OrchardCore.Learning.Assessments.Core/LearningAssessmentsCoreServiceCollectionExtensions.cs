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
    /// Registers the stores, the scorers of the thirteen question types, the attempt service and the deadline queue.
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
        services.TryAddScoped<GradingScope>();
        services.TryAddScoped<QtiFeedbackImageAccess>();
        services.TryAddScoped<GradingQueue>();
        services.AddGradingQueueSource<AttemptGradingQueueSource>();
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

        AddQuestionType<SingleChoiceScorer>(services, AssessmentsConstants.QuestionTypes.SingleChoice, AssessmentsConstants.ContentTypes.SingleChoiceQuestion, LocalizationSource.Create("Single choice", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<MultipleChoiceScorer>(services, AssessmentsConstants.QuestionTypes.MultipleChoice, AssessmentsConstants.ContentTypes.MultipleChoiceQuestion, LocalizationSource.Create("Multiple choice", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<TrueFalseScorer>(services, AssessmentsConstants.QuestionTypes.TrueFalse, AssessmentsConstants.ContentTypes.TrueFalseQuestion, LocalizationSource.Create("True or false", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<ShortTextScorer>(services, AssessmentsConstants.QuestionTypes.ShortText, AssessmentsConstants.ContentTypes.ShortTextQuestion, LocalizationSource.Create("Short text", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<NumericScorer>(services, AssessmentsConstants.QuestionTypes.Numeric, AssessmentsConstants.ContentTypes.NumericQuestion, LocalizationSource.Create("Numeric", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<MatchingScorer>(services, AssessmentsConstants.QuestionTypes.Matching, AssessmentsConstants.ContentTypes.MatchingQuestion, LocalizationSource.Create("Matching", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<OrderingScorer>(services, AssessmentsConstants.QuestionTypes.Ordering, AssessmentsConstants.ContentTypes.OrderingQuestion, LocalizationSource.Create("Ordering", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<FillInBlanksScorer>(services, AssessmentsConstants.QuestionTypes.FillInBlanks, AssessmentsConstants.ContentTypes.FillInBlanksQuestion, LocalizationSource.Create("Fill in the blanks", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<EssayScorer>(services, AssessmentsConstants.QuestionTypes.Essay, AssessmentsConstants.ContentTypes.EssayQuestion, LocalizationSource.Create("Essay", typeof(LearningAssessmentsCoreServiceCollectionExtensions)), manuallyGraded: true);
        AddQuestionType<SurveyScorer>(services, AssessmentsConstants.QuestionTypes.Survey, AssessmentsConstants.ContentTypes.SurveyQuestion, LocalizationSource.Create("Survey", typeof(LearningAssessmentsCoreServiceCollectionExtensions)), mayBeUngraded: true);
        AddQuestionType<DragIntoTextScorer>(services, AssessmentsConstants.QuestionTypes.DragIntoText, AssessmentsConstants.ContentTypes.DragIntoTextQuestion, LocalizationSource.Create("Drag into text", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<GapSelectScorer>(services, AssessmentsConstants.QuestionTypes.GapSelect, AssessmentsConstants.ContentTypes.GapSelectQuestion, LocalizationSource.Create("Select missing words", typeof(LearningAssessmentsCoreServiceCollectionExtensions)));
        AddQuestionType<DescriptionScorer>(services, AssessmentsConstants.QuestionTypes.Description, AssessmentsConstants.ContentTypes.DescriptionQuestion, LocalizationSource.Create("Description", typeof(LearningAssessmentsCoreServiceCollectionExtensions)), mayBeUngraded: true, informational: true);

        return services;
    }

    private static void AddQuestionType<TScorer>(IServiceCollection services, string name, string contentType, LocalizationSource displayName, bool manuallyGraded = false, bool mayBeUngraded = false, bool informational = false)
        where TScorer : class, IQuestionScorer
        => services.AddQuestionType<TScorer>(name, type =>
        {
            type.ContentType = contentType;
            type.DisplayName = displayName;
            type.ManuallyGraded = manuallyGraded;
            type.MayBeUngraded = mayBeUngraded;
            type.Informational = informational;
        });
}
