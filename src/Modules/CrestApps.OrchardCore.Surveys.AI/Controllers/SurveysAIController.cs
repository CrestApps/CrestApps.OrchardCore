using System.Globalization;
using CrestApps.OrchardCore.Surveys.AI.Services;
using CrestApps.OrchardCore.Surveys.AI.ViewModels;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.AI.Controllers;

/// <summary>
/// The AI pages and actions of a survey: scoring help for graders, a summary of written answers and an AI translation.
/// </summary>
[Admin]
public sealed class SurveysAIController : Controller
{
    private readonly ISurveyManager _surveys;
    private readonly ISurveyAuthorizationService _authorization;
    private readonly SurveyScoringAssistant _scoring;
    private readonly SurveyAnswerSummarizer _summarizer;
    private readonly SurveyTranslationAssistant _translator;
    private readonly ISurveysAICompleter _completer;
    private readonly ILocalizationService _localizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysAIController"/> class.
    /// </summary>
    /// <param name="surveys">The survey manager.</param>
    /// <param name="authorization">The survey authorization service.</param>
    /// <param name="scoring">The scoring assistant.</param>
    /// <param name="summarizer">The answer summarizer.</param>
    /// <param name="translator">The translation assistant.</param>
    /// <param name="completer">The AI completer.</param>
    /// <param name="localizationService">The localization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The localizer.</param>
    public SurveysAIController(
        ISurveyManager surveys,
        ISurveyAuthorizationService authorization,
        SurveyScoringAssistant scoring,
        SurveyAnswerSummarizer summarizer,
        SurveyTranslationAssistant translator,
        ISurveysAICompleter completer,
        ILocalizationService localizationService,
        INotifier notifier,
        IHtmlLocalizer<SurveysAIController> htmlLocalizer)
    {
        _surveys = surveys;
        _authorization = authorization;
        _scoring = scoring;
        _summarizer = summarizer;
        _translator = translator;
        _completer = completer;
        _localizationService = localizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Suggests a score and a comment for one answer that needs a grader.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="questionId">The question.</param>
    /// <returns>The suggestion as JSON.</returns>
    [HttpPost]
    [Admin("surveys/ai/scoring-help/{responseId}/{questionId}", "SurveysAIScoringHelp")]
    public async Task<IActionResult> ScoringHelp(string responseId, string questionId)
    {
        var result = await _scoring.SuggestAsync(User, responseId, questionId, HttpContext.RequestAborted);

        if (result.Error == SurveyScoringHelpResult.NotFound)
        {
            return NotFound();
        }

        return Json(new
        {
            error = result.Error switch
            {
                SurveyScoringHelpResult.NoModel => H["No AI model is set up for this site."].Value,
                SurveyScoringHelpResult.NoText => H["The answer has no text to score."].Value,
                SurveyScoringHelpResult.NoSuggestion => H["The AI model gave no suggestion. Try again."].Value,
                _ => null,
            },
            points = result.Suggestion?.Points,
            comment = result.Suggestion?.Comment,
        });
    }

    /// <summary>
    /// Shows the written questions of a survey, and the summary of one.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/ai/summarize", "SurveysAISummarize")]
    public async Task<IActionResult> Summarize(string id)
    {
        var model = await SummarizeModelAsync(id);

        return model is null ? Forbid() : View(model);
    }

    /// <summary>
    /// Summarizes the answers to one written question.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="questionId">The question.</param>
    /// <returns>The page with the summary.</returns>
    [HttpPost]
    [ActionName(nameof(Summarize))]
    [Admin("surveys/{id}/ai/summarize", "SurveysAISummarize")]
    public async Task<IActionResult> SummarizePost(string id, string questionId)
    {
        var model = await SummarizeModelAsync(id);

        if (model is null)
        {
            return Forbid();
        }

        var result = await _summarizer.SummarizeAsync(User, id, questionId, CultureInfo.CurrentUICulture.Name, HttpContext.RequestAborted);

        model.QuestionId = questionId;

        switch (result.Error)
        {
            case null:
                model.Summary = result.Summary;
                model.AnswerCount = result.AnswerCount;
                break;
            case SurveyAnswerSummaryResult.TooFew:
                await _notifier.WarningAsync(H["There are too few answers to summarize without risking that a respondent is recognized."]);
                break;
            case SurveyAnswerSummaryResult.NoModel:
                await _notifier.ErrorAsync(H["No AI model is set up for this site."]);
                break;
            default:
                return NotFound();
        }

        return View(model);
    }

    /// <summary>
    /// Shows the AI translation form of a survey.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/ai/translate", "SurveysAITranslate")]
    public async Task<IActionResult> Translate(string id)
    {
        var model = await TranslateModelAsync(id);

        return model is null ? Forbid() : View(model);
    }

    /// <summary>
    /// Translates the draft of a survey into a culture with AI.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="model">The posted form.</param>
    /// <returns>A redirect to the translations page.</returns>
    [HttpPost]
    [ActionName(nameof(Translate))]
    [Admin("surveys/{id}/ai/translate", "SurveysAITranslate")]
    public async Task<IActionResult> TranslatePost(string id, SurveyAITranslateViewModel model)
    {
        var page = await TranslateModelAsync(id);

        if (page is null)
        {
            return Forbid();
        }

        if (model is null || !page.Cultures.Any(item => string.Equals(item.Value, model.Culture, StringComparison.OrdinalIgnoreCase)))
        {
            await _notifier.ErrorAsync(H["Choose one of the site's cultures."]);

            return View(page);
        }

        var written = await _translator.TranslateAsync(User, id, model.Culture, model.Overwrite, HttpContext.RequestAborted);

        if (written is null)
        {
            await _notifier.ErrorAsync(H["The survey could not be translated. Check that an AI model is set up."]);

            return View(page);
        }

        await _notifier.SuccessAsync(H["{0} texts were translated into the draft. Review them, then publish the survey.", written.Value]);

        return Redirect(Url.Content($"~/Admin/surveys/{id}/translations?culture={Uri.EscapeDataString(model.Culture)}"));
    }

    private async Task<SurveyAISummarizeViewModel> SummarizeModelAsync(string id)
    {
        var survey = string.IsNullOrEmpty(id) ? null : await _surveys.FindByIdAsync(id, HttpContext.RequestAborted);

        if (survey is null || !await _authorization.AuthorizeAsync(User, survey, SurveyOperation.ViewResponses, HttpContext.RequestAborted))
        {
            return null;
        }

        return new SurveyAISummarizeViewModel
        {
            Survey = survey,
            Questions = (await _summarizer.ListTextQuestionsAsync(survey, HttpContext.RequestAborted))
                .Select(question => new SelectListItem(question.Title ?? question.Name, question.Id))
                .ToList(),
            ModelAvailable = await _completer.IsAvailableAsync(),
        };
    }

    private async Task<SurveyAITranslateViewModel> TranslateModelAsync(string id)
    {
        var survey = string.IsNullOrEmpty(id) ? null : await _surveys.FindByIdAsync(id, HttpContext.RequestAborted);

        if (survey is null || !await _authorization.AuthorizeAsync(User, survey, SurveyOperation.Manage, HttpContext.RequestAborted))
        {
            return null;
        }

        var supported = await _localizationService.GetSupportedCulturesAsync();
        var defaultCulture = await _localizationService.GetDefaultCultureAsync();

        return new SurveyAITranslateViewModel
        {
            Survey = survey,
            Cultures = (supported ?? [])
                .Where(name => !string.IsNullOrWhiteSpace(name) && !string.Equals(name, defaultCulture, StringComparison.OrdinalIgnoreCase))
                .Select(name => new SelectListItem(SurveyAnswerSummarizer.LanguageName(name), name))
                .ToList(),
            ModelAvailable = await _completer.IsAvailableAsync(),
        };
    }
}
