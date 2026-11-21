using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// The outcome of an authoring request.
/// </summary>
public sealed class AuthoringResult
{
    /// <summary>
    /// No AI deployment is set up.
    /// </summary>
    public const string NoModel = "NoModel";

    /// <summary>
    /// The model's answer held nothing usable.
    /// </summary>
    public const string NoDraft = "NoDraft";

    /// <summary>
    /// The lesson, bank or item was not found.
    /// </summary>
    public const string NotFound = "NotFound";

    /// <summary>
    /// Gets or sets the error, or <see langword="null"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets whether drafts were made.
    /// </summary>
    public bool Succeeded => Error is null;

    /// <summary>
    /// Gets or sets the drafts made: the course, the questions, the rubric or the translation.
    /// </summary>
    public IReadOnlyList<string> ContentItemIds { get; set; } = [];

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static AuthoringResult Fail(string error)
        => new() { Error = error };
}

/// <summary>
/// The author prompts: a course outline, questions from a lesson, a rubric and a translation, each saved as unpublished
/// drafts for an author to review and publish. Nothing it makes is published. Callers check the person's permissions.
/// </summary>
public sealed class LearningAuthoringAssistant
{
    private readonly ILearningAICompleter _completer;
    private readonly IContentManager _contentManager;
    private readonly IHtmlSanitizerService _htmlSanitizer;
    private readonly IQuestionBankManager _bankManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningAuthoringAssistant"/> class.
    /// </summary>
    /// <param name="completer">The AI completer.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="htmlSanitizer">The HTML sanitizer.</param>
    /// <param name="bankManagers">The question bank manager of Surveys, when Surveys is on.</param>
    public LearningAuthoringAssistant(
        ILearningAICompleter completer,
        IContentManager contentManager,
        IHtmlSanitizerService htmlSanitizer,
        IEnumerable<IQuestionBankManager> bankManagers)
    {
        _completer = completer;
        _contentManager = contentManager;
        _htmlSanitizer = htmlSanitizer;
        _bankManager = bankManagers.FirstOrDefault();
    }

    /// <summary>
    /// Drafts a course with an outline of draft page lessons.
    /// </summary>
    public async Task<AuthoringResult> DraftOutlineAsync(string topic, string audience, int lessonCount, CancellationToken cancellationToken)
    {
        var answer = await _completer.CompleteAsync(
            LearningAIConstants.Templates.DraftOutline,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Audience"] = audience ?? string.Empty,
                ["LessonCount"] = Math.Clamp(lessonCount, 1, 30),
            },
            topic,
            cancellationToken);

        if (answer is null)
        {
            return AuthoringResult.Fail(AuthoringResult.NoModel);
        }

        var outline = AuthoringDraftParser.ParseOutline(answer);

        if (outline is null)
        {
            return AuthoringResult.Fail(AuthoringResult.NoDraft);
        }

        var course = await _contentManager.NewAsync(LearningConstants.ContentTypes.Course);
        course.DisplayText = outline.Title;
        course.Alter<TitlePart>(part => part.Title = outline.Title);
        course.Alter<CoursePart>(part => part.Summary = outline.Summary);
        await _contentManager.CreateAsync(course, VersionOptions.Draft);

        var sections = new List<CourseSection>();
        var index = 0;

        foreach (var section in outline.Sections)
        {
            var items = new List<CourseOutlineItem>();

            foreach (var lesson in section.Lessons)
            {
                var item = await _contentManager.NewAsync(LearningConstants.ContentTypes.PageLesson);
                item.DisplayText = lesson.Title;
                item.Alter<TitlePart>(part => part.Title = lesson.Title);
                item.Alter<HtmlBodyPart>(part => part.Html = AuthoringDraftBuilder.Paragraphs(lesson.Summary));
                item.Alter<ContainedPart>(part =>
                {
                    part.ListContentItemId = course.ContentItemId;
                    part.ListContentType = course.ContentType;
                });
                await _contentManager.CreateAsync(item, VersionOptions.Draft);

                items.Add(new CourseOutlineItem { ContentItemId = item.ContentItemId });
            }

            sections.Add(new CourseSection
            {
                Id = "s" + (++index).ToString(CultureInfo.InvariantCulture),
                Title = section.Title,
                Items = items,
            });
        }

        course.Alter<CourseOutlinePart>(part => part.Sections = sections);
        await _contentManager.UpdateAsync(course);

        return new AuthoringResult { ContentItemIds = [course.ContentItemId] };
    }

    /// <summary>
    /// Drafts questions about a lesson in a question bank.
    /// </summary>
    public async Task<AuthoringResult> DraftQuestionsAsync(ContentItem lesson, QuestionBank bank, int count, CancellationToken cancellationToken)
    {
        if (lesson is null || bank is null || _bankManager is null)
        {
            return AuthoringResult.Fail(AuthoringResult.NotFound);
        }

        var input = new JsonObject
        {
            ["title"] = lesson.DisplayText,
            ["lesson"] = QuizResultExplainer.Text(lesson.Get<HtmlBodyPart>(nameof(HtmlBodyPart))?.Html) ?? string.Empty,
        };

        var answer = await _completer.CompleteAsync(
            LearningAIConstants.Templates.DraftQuestions,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Count"] = Math.Clamp(count, 1, AuthoringDraftParser.MaxQuestions),
            },
            input.ToJsonString(),
            cancellationToken);

        if (answer is null)
        {
            return AuthoringResult.Fail(AuthoringResult.NoModel);
        }

        var questions = AuthoringDraftParser.ParseQuestions(answer);

        if (questions.Count == 0)
        {
            return AuthoringResult.Fail(AuthoringResult.NoDraft);
        }

        var drafts = new List<BankQuestion>();

        foreach (var question in questions)
        {
            if (AuthoringDraftBuilder.BuildQuestion(question) is not { } built)
            {
                continue;
            }

            var draft = await _bankManager.NewQuestionAsync(bank.ItemId, built.ControlType, cancellationToken);

            draft.Question = built;
            draft.Retired = true;
            drafts.Add(draft);
        }

        // Drafted questions are retired until an author reviews them, so no pool draws them before then.
        var summary = await _bankManager.ImportAsync(bank.ItemId, drafts, cancellationToken);

        if (summary.Created + summary.Updated == 0)
        {
            return AuthoringResult.Fail(AuthoringResult.NoDraft);
        }

        var ids = drafts.Select(draft => draft.ItemId).ToList();

        return new AuthoringResult { ContentItemIds = ids };
    }

    /// <summary>
    /// Drafts a rubric.
    /// </summary>
    public async Task<AuthoringResult> DraftRubricAsync(string task, int criteria, CancellationToken cancellationToken)
    {
        var answer = await _completer.CompleteAsync(
            LearningAIConstants.Templates.DraftRubric,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["CriteriaCount"] = Math.Clamp(criteria, 1, AuthoringDraftParser.MaxCriteria),
            },
            task,
            cancellationToken);

        if (answer is null)
        {
            return AuthoringResult.Fail(AuthoringResult.NoModel);
        }

        var rubric = AuthoringDraftParser.ParseRubric(answer);

        if (rubric is null)
        {
            return AuthoringResult.Fail(AuthoringResult.NoDraft);
        }

        var item = await _contentManager.NewAsync(CourseworkConstants.ContentTypes.Rubric);
        AuthoringDraftBuilder.ApplyRubric(item, rubric);
        await _contentManager.CreateAsync(item, VersionOptions.Draft);

        return new AuthoringResult { ContentItemIds = [item.ContentItemId] };
    }

    /// <summary>
    /// Drafts a translated copy of a lesson, in the same course list, unpublished.
    /// </summary>
    public async Task<AuthoringResult> TranslateAsync(ContentItem source, string language, CancellationToken cancellationToken)
    {
        if (source is null)
        {
            return AuthoringResult.Fail(AuthoringResult.NotFound);
        }

        var input = new JsonObject
        {
            ["title"] = source.DisplayText,
            ["body"] = source.Get<HtmlBodyPart>(nameof(HtmlBodyPart))?.Html ?? string.Empty,
        };

        var answer = await _completer.CompleteAsync(
            LearningAIConstants.Templates.Translate,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Language"] = language ?? string.Empty,
            },
            input.ToJsonString(),
            cancellationToken);

        if (answer is null)
        {
            return AuthoringResult.Fail(AuthoringResult.NoModel);
        }

        var translation = AuthoringDraftParser.ParseTranslation(answer);

        if (translation is null)
        {
            return AuthoringResult.Fail(AuthoringResult.NoDraft);
        }

        var copy = await _contentManager.CloneAsync(source);
        copy.DisplayText = translation.Title;
        copy.Alter<TitlePart>(part => part.Title = translation.Title);
        copy.Alter<HtmlBodyPart>(part => part.Html = _htmlSanitizer.Sanitize(translation.Body));
        await _contentManager.UpdateAsync(copy);

        return new AuthoringResult { ContentItemIds = [copy.ContentItemId] };
    }
}
