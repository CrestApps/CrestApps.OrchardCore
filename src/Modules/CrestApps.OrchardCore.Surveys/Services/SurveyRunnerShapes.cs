using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.ViewModels;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using OrchardCore.DisplayManagement;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Builds the runner shapes: <c>SurveyPage_Respond</c> (alternate by survey name), the <c>SurveyQuestion_Respond</c>
/// wrapper of each question, and the <c>SurveyControl</c> shape of its type with the display type Respond or Review and
/// the alternates <c>SurveyControl_{DisplayType}__{ControlType}</c>, <c>__{SurveyName}</c> and <c>__{QuestionName}</c>.
/// </summary>
public sealed class SurveyRunnerShapes
{
    /// <summary>
    /// The display type of the respondent's form.
    /// </summary>
    public const string Respond = SurveyControlShapes.Respond;

    /// <summary>
    /// The display type of the read-only review.
    /// </summary>
    public const string Review = SurveyControlShapes.Review;

    private readonly IShapeFactory _shapeFactory;
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyLogicEngine _logic;
    private readonly SurveyPageViewBuilder _pageViews;
    private readonly IResourceManager _resourceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRunnerShapes"/> class.
    /// </summary>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="registry">The control registry.</param>
    /// <param name="logic">The logic engine.</param>
    /// <param name="pageViews">The page view builder.</param>
    /// <param name="resourceManager">The resource manager, which loads the scripts of the types on a page.</param>
    public SurveyRunnerShapes(
        IShapeFactory shapeFactory,
        ISurveyControlRegistry registry,
        SurveyLogicEngine logic,
        SurveyPageViewBuilder pageViews,
        IResourceManager resourceManager)
    {
        _shapeFactory = shapeFactory;
        _registry = registry;
        _logic = logic;
        _pageViews = pageViews;
        _resourceManager = resourceManager;
    }

    /// <summary>
    /// Builds the page shape of a screen.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="version">The version.</param>
    /// <param name="response">The response.</param>
    /// <param name="page">The screen view.</param>
    /// <param name="errors">The errors by question id.</param>
    /// <param name="message">The message of the post, or <see langword="null"/>.</param>
    /// <param name="preview">Whether this is a staff preview.</param>
    /// <param name="showHidden">Whether a preview shows hidden questions.</param>
    /// <returns>The shape.</returns>
    public async Task<IShape> BuildPageAsync(Survey survey, SurveyVersion version, SurveyResponse response, SurveyPageView page, IReadOnlyDictionary<string, List<string>> errors, string message, bool preview, bool showHidden)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(page);

        var definition = version.Definition;
        var questions = new List<IShape>();
        var uploadsFiles = false;

        foreach (var view in page.Questions)
        {
            var question = Find(definition, view.Id);

            if (question is null)
            {
                continue;
            }

            uploadsFiles |= _registry.GetControl(question.ControlType)?.Has(SurveyControlCapabilities.StoresFiles) == true;

            response.Answers.TryGetValue(view.Id, out var answer);
            var questionErrors = errors is not null && errors.TryGetValue(view.Id, out var list) ? list : [];
            var hidden = page.HiddenQuestionIds.Contains(view.Id);
            var checkedAnswer = answer?.CheckedUtc is not null;

            questions.Add(await BuildQuestionAsync(
                survey,
                question,
                view,
                answer,
                questionErrors,
                hidden && !showHidden,
                readOnly: view.Disabled || checkedAnswer,
                Respond,
                hiddenChoiceIds: page.HiddenChoiceIds.TryGetValue(view.Id, out var hiddenChoices) ? hiddenChoices : null,
                canCheck: definition.Scoring?.FeedbackMode == FeedbackMode.Immediate && !checkedAnswer,
                culture: response.Culture,
                locked: checkedAnswer));
        }

        var shape = await _shapeFactory.CreateAsync<SurveyPageRespondViewModel>("SurveyPage_Respond", model =>
        {
            model.Survey = survey;
            model.Title = string.IsNullOrWhiteSpace(definition.Title) ? survey.Title : definition.Title;
            model.Response = response;
            model.Page = page;
            model.Presentation = definition.Presentation ?? new SurveyPresentationSettings();
            model.Questions = questions;
            model.Message = message;
            model.ImmediateFeedback = definition.Scoring?.FeedbackMode == FeedbackMode.Immediate;
            model.ConfirmBeforeSubmit = definition.Navigation?.ConfirmBeforeSubmit == true;
            model.IsPreview = preview;
            model.ShowHidden = showHidden;
            model.UploadsFiles = uploadsFiles;

            if (preview)
            {
                foreach (var screen in response.Plan.PageOrder)
                {
                    var pageId = SurveyPlanNavigator.PageOf(response.Plan, screen);
                    var designed = definition.Pages.FirstOrDefault(item => item.Id == pageId);

                    model.Screens.Add(new KeyValuePair<string, string>(screen, string.IsNullOrWhiteSpace(designed?.Title) ? designed?.Name ?? screen : designed.Title));
                }
            }
        });

        if (!string.IsNullOrEmpty(survey.Name))
        {
            shape.Metadata.Alternates.Add("SurveyPage_Respond__" + SurveyControlShapes.Encode(survey.Name));
        }

        return shape;
    }

    /// <summary>
    /// Builds the review shapes of a completed response, in the order the respondent saw the questions.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="version">The version.</param>
    /// <param name="response">The response.</param>
    /// <param name="shows">What the review shows.</param>
    /// <param name="forRespondent">Whether the respondent sees the review. Hidden questions hold values the respondent
    /// never saw nor typed (from the link, a consumer or a workflow), so only staff see them.</param>
    /// <returns>The question shapes.</returns>
    public async Task<IList<IShape>> BuildReviewAsync(Survey survey, SurveyVersion version, SurveyResponse response, ReviewShows shows, bool forRespondent = false)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(response);

        var definition = version.Definition;
        var state = _logic.Evaluate(definition, response, SurveyRuntimeVersions.CacheKey(version));
        var shapes = new List<IShape>();
        var screens = response.PagePath.Count > 0 ? SurveyScoringEngine.ScreensOnPath(response).ToList() : response.Plan.PageOrder;

        // The questions on the respondent's path that logic did not hide, then each locked answer (checked with immediate
        // feedback) that logic took off the path or hid: the respondent saw it graded and it counts toward the score, so
        // the review shows it too. The respondent can't do that with their own answers (a change that would is refused),
        // so this is only the author's logic, such as a rule on the date or time.
        var listed = new HashSet<string>(StringComparer.Ordinal);
        var questionIds = screens
            .Distinct(StringComparer.Ordinal)
            .SelectMany(screen => SurveyPlanNavigator.QuestionsOf(response.Plan, screen))
            .Where(questionId => !state.HiddenQuestionIds.Contains(questionId) || response.Answers.GetValueOrDefault(questionId)?.IsLocked == true)
            .Concat(response.Plan.PageOrder
                .SelectMany(screen => SurveyPlanNavigator.QuestionsOf(response.Plan, screen))
                .Where(questionId => response.Answers.GetValueOrDefault(questionId)?.IsLocked == true))
            .Where(listed.Add)
            .ToList();

        foreach (var questionId in questionIds)
        {
            var question = Find(definition, questionId);
            var handler = question is null ? null : _registry.GetHandler(question.ControlType);

            if (handler is null
                || (forRespondent && _registry.GetControl(question.ControlType)?.Has(SurveyControlCapabilities.NotShownToRespondents) == true))
            {
                continue;
            }

            response.Answers.TryGetValue(questionId, out var answer);

            var view = _pageViews.Project(definition, response, state, question, handler, null, state.HiddenQuestionIds);
            var feedback = Feedback(question, answer, shows);
            var showsAnswer = (shows & ReviewShows.Responses) != 0 && answer is not null;

            // The review templates of text-like types show the answer as the type itself formats it.
            var answerText = showsAnswer
                ? handler.FormatAnswer(question, answer, new SurveyFormatContext { Culture = CultureInfo.CurrentCulture, Definition = definition })
                : null;

            shapes.Add(await BuildQuestionAsync(
                survey,
                question,
                view,
                (shows & ReviewShows.Responses) != 0 ? answer : null,
                [],
                hidden: false,
                readOnly: true,
                Review,
                hiddenChoiceIds: null,
                canCheck: false,
                culture: response.Culture,
                feedback: feedback,
                answerText: answerText));
        }

        return shapes;
    }

    private SurveyAnswerFeedback Feedback(SurveyQuestion question, SurveyAnswer answer, ReviewShows shows)
    {
        var scorer = _registry.GetScorer(question.ControlType);

        if (scorer is null || (shows & (ReviewShows.RightAnswers | ReviewShows.Feedback | ReviewShows.GeneralFeedback)) == 0)
        {
            return null;
        }

        var feedback = scorer.GetFeedback(question, answer);

        if (feedback is null)
        {
            return null;
        }

        return new SurveyAnswerFeedback
        {
            Correct = (shows & (ReviewShows.RightAnswers | ReviewShows.Feedback)) != 0 ? feedback.Correct : null,
            RightAnswer = (shows & ReviewShows.RightAnswers) != 0 ? feedback.RightAnswer : null,
            CorrectChoiceIds = (shows & ReviewShows.RightAnswers) != 0 ? feedback.CorrectChoiceIds : [],
            Feedback = (shows & ReviewShows.Feedback) != 0 ? feedback.Feedback : null,
            ChoiceFeedback = (shows & ReviewShows.Feedback) != 0 ? feedback.ChoiceFeedback : [],
            GeneralFeedback = (shows & ReviewShows.GeneralFeedback) != 0 ? feedback.GeneralFeedback : null,
        };
    }

    private async Task<IShape> BuildQuestionAsync(
        Survey survey,
        SurveyQuestion question,
        SurveyQuestionView view,
        SurveyAnswer answer,
        IReadOnlyList<string> errors,
        bool hidden,
        bool readOnly,
        string displayType,
        IReadOnlyCollection<string> hiddenChoiceIds,
        bool canCheck,
        string culture,
        SurveyAnswerFeedback feedback = null,
        string answerText = null,
        bool locked = false)
    {
        var control = await _shapeFactory.CreateAsync<SurveyControlViewModel>(SurveyControlShapes.Control, model =>
        {
            model.Question = view;
            model.Settings = view.Settings;
            model.Answer = answer;
            model.Prefix = SurveysConstants.AnswerPrefix + "." + view.Id;
            model.Choices = view.Choices;
            model.Errors = errors;
            model.ReadOnly = readOnly;
            model.Feedback = feedback;
            model.Culture = culture;
            model.AnswerText = answerText;
        });

        control.Metadata.DisplayType = displayType;

        foreach (var alternate in SurveyControlShapes.GetAlternates(displayType, question.ControlType, survey?.Name, question.Name))
        {
            control.Metadata.Alternates.Add(alternate);
        }

        if (hiddenChoiceIds is { Count: > 0 })
        {
            control.Properties["HiddenChoiceIds"] = hiddenChoiceIds;
        }

        var descriptor = _registry.GetControl(question.ControlType);

        // A type with its own UI names its script; the runner loads it once for the page, so the Respond view adds no
        // script tag of its own.
        if (displayType == Respond
            && descriptor is not null
            && descriptor.Has(SurveyControlCapabilities.ClientScript)
            && !string.IsNullOrEmpty(descriptor.ScriptResourceName))
        {
            _resourceManager.RegisterResource("script", descriptor.ScriptResourceName).AtFoot();
        }

        return await _shapeFactory.CreateAsync<SurveyQuestionRespondViewModel>("SurveyQuestion_Respond", model =>
        {
            model.Question = view;
            model.Control = control;
            model.Errors = errors;
            model.Feedback = feedback;
            model.Hidden = hidden;
            model.CanCheck = canCheck;
            model.Locked = locked;
            model.Answerable = descriptor?.Has(SurveyControlCapabilities.Answerable) ?? true;
            model.DisplayType = displayType;
        });
    }

    private static SurveyQuestion Find(SurveyDefinition definition, string questionId)
        => definition.Pages?.SelectMany(page => page.Questions ?? []).FirstOrDefault(question => question?.Id == questionId);
}
