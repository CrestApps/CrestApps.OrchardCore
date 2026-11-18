using System.Security.Claims;
using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.AI.ViewModels;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Coursework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.AI.Controllers;

/// <summary>
/// The authoring assistant: author prompts that draft a course outline, questions from a lesson, a rubric or a
/// translation. Every result is saved as an unpublished draft for the author to review; nothing is published here.
/// </summary>
[Admin]
[Feature(LearningAIConstants.Features.Authoring)]
public sealed class AuthoringController : Controller
{
    private const int MaxListed = 100;

    private static readonly string[] _lessonTypes =
    [
        LearningConstants.ContentTypes.PageLesson,
        LearningConstants.ContentTypes.VideoLesson,
        LearningConstants.ContentTypes.FileLesson,
    ];

    private readonly IAuthorizationService _authorizationService;
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly ILearningAICompleter _completer;
    private readonly LearningAuthoringAssistant _assistant;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthoringController"/> class.
    /// </summary>
    public AuthoringController(
        IAuthorizationService authorizationService,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        ILearningAICompleter completer,
        LearningAuthoringAssistant assistant,
        INotifier notifier,
        IHtmlLocalizer<AuthoringController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _completer = completer;
        _assistant = assistant;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows the author prompts.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("learning/ai/authoring", "LearningAIAuthoring")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningAIPermissions.UseAuthoringAssistant))
        {
            return Forbid();
        }

        return View(await BuildAsync(new AuthoringViewModel()));
    }

    /// <summary>
    /// Drafts a course with an outline of draft lessons.
    /// </summary>
    /// <param name="model">The topic.</param>
    /// <returns>The course draft's editor, or the page with a message.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/ai/authoring/outline", "LearningAIAuthoringOutline")]
    public async Task<IActionResult> Outline(AuthoringViewModel model)
    {
        var course = await NewOwnedAsync(LearningConstants.ContentTypes.Course);

        if (!await CanUseAsync() || !await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, course))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.Topic))
        {
            await _notifier.WarningAsync(H["Say what the course is about."]);

            return View(nameof(Index), await BuildAsync(model));
        }

        var result = await _assistant.DraftOutlineAsync(model.Topic.Trim(), model.Audience?.Trim(), model.LessonCount, HttpContext.RequestAborted);

        return await FinishAsync(result, model, H["A draft course with its outline and draft lessons was made. Nothing is published: review it, then publish the course and its lessons."]);
    }

    /// <summary>
    /// Drafts questions from a lesson into a question bank.
    /// </summary>
    /// <param name="model">The lesson and the bank.</param>
    /// <returns>The bank's editor, or the page with a message.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/ai/authoring/questions", "LearningAIAuthoringQuestions")]
    public async Task<IActionResult> Questions(AuthoringViewModel model)
    {
        var lesson = string.IsNullOrEmpty(model.LessonId) ? null : await _contentManager.GetAsync(model.LessonId, VersionOptions.Latest);
        var bank = string.IsNullOrEmpty(model.BankId) ? null : await _contentManager.GetAsync(model.BankId, VersionOptions.Latest);

        if (!await CanUseAsync() || !await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        if (lesson is null || bank is null || bank.ContentType != AssessmentsConstants.ContentTypes.QuestionBank)
        {
            await _notifier.WarningAsync(H["Pick a lesson and a question bank."]);

            return View(nameof(Index), await BuildAsync(model));
        }

        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, lesson) ||
            !await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, bank))
        {
            return Forbid();
        }

        var result = await _assistant.DraftQuestionsAsync(lesson, bank, model.QuestionCount, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["{0} draft questions were added to the bank. None is published: review each one, then publish it.", result.ContentItemIds.Count]);

            return EditorOf(bank.ContentItemId);
        }

        return await FinishAsync(result, model, null);
    }

    /// <summary>
    /// Drafts a rubric.
    /// </summary>
    /// <param name="model">The work the rubric grades.</param>
    /// <returns>The rubric draft's editor, or the page with a message.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/ai/authoring/rubric", "LearningAIAuthoringRubric")]
    public async Task<IActionResult> Rubric(AuthoringViewModel model)
    {
        if (!await CanUseAsync() || await _contentDefinitionManager.GetTypeDefinitionAsync(CourseworkConstants.ContentTypes.Rubric) is null)
        {
            return Forbid();
        }

        var rubric = await NewOwnedAsync(CourseworkConstants.ContentTypes.Rubric);

        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, rubric))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.Task))
        {
            await _notifier.WarningAsync(H["Describe the work the rubric grades."]);

            return View(nameof(Index), await BuildAsync(model));
        }

        var result = await _assistant.DraftRubricAsync(model.Task.Trim(), model.CriteriaCount, HttpContext.RequestAborted);

        return await FinishAsync(result, model, H["A draft rubric was made. It is not published: review it, then publish it."]);
    }

    /// <summary>
    /// Drafts a translated copy of a lesson.
    /// </summary>
    /// <param name="model">The lesson and the language.</param>
    /// <returns>The translation draft's editor, or the page with a message.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/ai/authoring/translate", "LearningAIAuthoringTranslate")]
    public async Task<IActionResult> Translate(AuthoringViewModel model)
    {
        var lesson = string.IsNullOrEmpty(model.LessonId) ? null : await _contentManager.GetAsync(model.LessonId, VersionOptions.Latest);

        if (!await CanUseAsync())
        {
            return Forbid();
        }

        if (lesson is null || string.IsNullOrWhiteSpace(model.Language))
        {
            await _notifier.WarningAsync(H["Pick a lesson and say which language to translate it into."]);

            return View(nameof(Index), await BuildAsync(model));
        }

        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, lesson))
        {
            return Forbid();
        }

        var result = await _assistant.TranslateAsync(lesson, model.Language.Trim(), HttpContext.RequestAborted);

        return await FinishAsync(result, model, H["A draft translated copy of the lesson was made. It is not published and not in the course outline yet: review it, then add it to the outline and publish it."]);
    }

    private async Task<IActionResult> FinishAsync(AuthoringResult result, AuthoringViewModel model, LocalizedHtmlString success)
    {
        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);

            return EditorOf(result.ContentItemIds[0]);
        }

        if (result.Error == AuthoringResult.NoModel)
        {
            await _notifier.WarningAsync(H["No AI model is set up for this site, so nothing was drafted. An administrator sets up an AI connection and deployment first."]);
        }
        else if (result.Error == AuthoringResult.NotFound)
        {
            await _notifier.WarningAsync(H["The lesson or question bank was not found."]);
        }
        else
        {
            await _notifier.WarningAsync(H["The model's answer could not be used, so nothing was saved. Try again."]);
        }

        return View(nameof(Index), await BuildAsync(model));
    }

    private RedirectToActionResult EditorOf(string contentItemId)
        => RedirectToAction("Edit", "Admin", new { area = "OrchardCore.Contents", contentItemId });

    private async Task<bool> CanUseAsync()
        => await _authorizationService.AuthorizeAsync(User, LearningAIPermissions.UseAuthoringAssistant);

    private async Task<ContentItem> NewOwnedAsync(string contentType)
    {
        var item = await _contentManager.NewAsync(contentType);
        item.Owner = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return item;
    }

    private async Task<AuthoringViewModel> BuildAsync(AuthoringViewModel model)
    {
        model.ModelAvailable = await _completer.IsAvailableAsync();
        model.CanDraftQuestions = await _contentDefinitionManager.GetTypeDefinitionAsync(AssessmentsConstants.ContentTypes.QuestionBank) is not null
            && await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageQuestionBanks);
        model.CanDraftRubrics = await _contentDefinitionManager.GetTypeDefinitionAsync(CourseworkConstants.ContentTypes.Rubric) is not null;

        var lessons = await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(_lessonTypes))
            .OrderByDescending(index => index.ModifiedUtc)
            .Take(MaxListed)
            .ListAsync(HttpContext.RequestAborted);

        // Only lessons the author may edit: the drafts are made from them (review round 9).
        var editable = new List<ContentItem>();

        foreach (var lesson in lessons)
        {
            if (await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, lesson))
            {
                editable.Add(lesson);
            }
        }

        model.Lessons = editable
            .Select(item => new SelectListItem(item.DisplayText ?? item.ContentItemId, item.ContentItemId, item.ContentItemId == model.LessonId))
            .ToList();

        if (model.CanDraftQuestions)
        {
            var banks = await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType == AssessmentsConstants.ContentTypes.QuestionBank)
                .OrderBy(index => index.DisplayText)
                .Take(MaxListed)
                .ListAsync(HttpContext.RequestAborted);

            model.Banks = banks
                .Select(item => new SelectListItem(item.DisplayText ?? item.ContentItemId, item.ContentItemId, item.ContentItemId == model.BankId))
                .ToList();
        }

        return model;
    }
}
