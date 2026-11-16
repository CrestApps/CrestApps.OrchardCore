using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Migrations;

/// <summary>
/// Creates the assessment index tables, the question types, the question bank and the quiz course item.
/// </summary>
internal sealed class AssessmentsMigrations : DataMigration
{
    private static readonly (string Type, string DisplayName, string Part)[] _questionTypes =
    [
        (AssessmentsConstants.ContentTypes.SingleChoiceQuestion, "Single choice question", nameof(ChoiceQuestionPart)),
        (AssessmentsConstants.ContentTypes.MultipleChoiceQuestion, "Multiple choice question", nameof(ChoiceQuestionPart)),
        (AssessmentsConstants.ContentTypes.TrueFalseQuestion, "True or false question", nameof(TrueFalseQuestionPart)),
        (AssessmentsConstants.ContentTypes.ShortTextQuestion, "Short text question", nameof(ShortTextQuestionPart)),
        (AssessmentsConstants.ContentTypes.NumericQuestion, "Numeric question", nameof(NumericQuestionPart)),
        (AssessmentsConstants.ContentTypes.MatchingQuestion, "Matching question", nameof(MatchingQuestionPart)),
        (AssessmentsConstants.ContentTypes.OrderingQuestion, "Ordering question", nameof(OrderingQuestionPart)),
        (AssessmentsConstants.ContentTypes.FillInBlanksQuestion, "Fill in the blanks question", nameof(FillInBlanksQuestionPart)),
        (AssessmentsConstants.ContentTypes.EssayQuestion, "Essay question", nameof(EssayQuestionPart)),
    ];

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;

    public AssessmentsMigrations(IContentDefinitionManager contentDefinitionManager, CourseListSynchronizer courseListSynchronizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
    }

    public async Task<int> CreateAsync()
    {
        await AssessmentsIndexSchema.CreateAsync(SchemaBuilder);
        await QuestionSourceIndexProvider.CreateTableAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(QuestionPart), part => part
            .WithDisplayName("Question")
            .WithDescription("The prompt, points, difficulty, tags and general feedback of a question."));
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(ChoiceQuestionPart), part => part
            .WithDisplayName("Choice question")
            .WithDescription("The options of a single or multiple choice question."));
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(TrueFalseQuestionPart), part => part.WithDisplayName("True or false question"));
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(ShortTextQuestionPart), part => part.WithDisplayName("Short text question"));
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(NumericQuestionPart), part => part.WithDisplayName("Numeric question"));
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(MatchingQuestionPart), part => part.WithDisplayName("Matching question"));
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(OrderingQuestionPart), part => part.WithDisplayName("Ordering question"));
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(FillInBlanksQuestionPart), part => part.WithDisplayName("Fill in the blanks question"));
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(EssayQuestionPart), part => part.WithDisplayName("Essay question"));
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(QuizPart), part => part
            .WithDisplayName("Quiz")
            .WithDescription("Makes a course item a quiz: its questions, draws, time limit, attempts, pass mark, window and review policy."));

        foreach (var (type, displayName, part) in _questionTypes)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(type, builder => builder
                .WithDisplayName(displayName)
                .Creatable()
                .Listable()
                .Draftable()
                .Versionable()
                .Securable()
                .WithPart(nameof(TitlePart), titlePart => titlePart.WithPosition("1"))
                .WithPart(nameof(QuestionPart), questionPart => questionPart.WithPosition("2"))
                .WithPart(part, typePart => typePart.WithPosition("3")));
        }

        await _contentDefinitionManager.AlterTypeDefinitionAsync(AssessmentsConstants.ContentTypes.QuestionBank, type => type
            .WithDisplayName("Question bank")
            .Creatable()
            .Listable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(HtmlBodyPart), part => part.WithPosition("2").WithEditor("Wysiwyg"))
            .WithPart(nameof(ListPart), part => part
                .WithPosition("3")
                .WithSettings(new ListPartSettings
                {
                    PageSize = 50,
                    ShowHeader = true,
                    ContainedContentTypes = _questionTypes.Select(entry => entry.Type).ToArray(),
                })));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(AssessmentsConstants.ContentTypes.Quiz, type => type
            .WithDisplayName("Quiz")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(HtmlBodyPart), part => part.WithPosition("2").WithEditor("Wysiwyg"))
            .WithPart(nameof(QuizPart), part => part.WithPosition("3"))
            .WithPart(nameof(CourseItemPart), part => part
                .WithPosition("10")
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = AssessmentsConstants.QuizKind,
                    DefaultCompletionMode = CompletionModes.Passed,
                })));

        await _courseListSynchronizer.SynchronizeAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index of imported questions by their source identifier (QTI import).
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await QuestionSourceIndexProvider.CreateTableAsync(SchemaBuilder);

        return 2;
    }
}
