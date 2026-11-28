using System.Globalization;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Localization;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The designer: the outline of a draft, the question and page editors, the survey-wide settings, publishing and
/// discarding the draft.
/// </summary>
[Admin]
public sealed class SurveyDesignerController : Controller
{
    private readonly ISurveyManager _manager;
    private readonly ISurveyPublisher _publisher;
    private readonly ISurveyAuthorizationService _surveyAuthorization;
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyDraftEditor _draftEditor;
    private readonly SurveyDesignerContext _designerContext;
    private readonly IDisplayManager<SurveyQuestion> _questionDisplayManager;
    private readonly IDisplayManager<SurveyDefinition> _definitionDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly QuestionBankStore _bankStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHtmlSanitizerService _htmlSanitizer;
    private readonly INotifier _notifier;

    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyDesignerController"/> class.
    /// </summary>
    public SurveyDesignerController(
        ISurveyManager manager,
        ISurveyPublisher publisher,
        ISurveyAuthorizationService surveyAuthorization,
        ISurveyControlRegistry registry,
        SurveyDraftEditor draftEditor,
        SurveyDesignerContext designerContext,
        IDisplayManager<SurveyQuestion> questionDisplayManager,
        IDisplayManager<SurveyDefinition> definitionDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        QuestionBankStore bankStore,
        IAuthorizationService authorizationService,
        IHtmlSanitizerService htmlSanitizer,
        INotifier notifier,
        IStringLocalizerFactory localizerFactory,
        IHtmlLocalizer<SurveyDesignerController> htmlLocalizer,
        IStringLocalizer<SurveyDesignerController> stringLocalizer)
    {
        _manager = manager;
        _publisher = publisher;
        _surveyAuthorization = surveyAuthorization;
        _registry = registry;
        _draftEditor = draftEditor;
        _designerContext = designerContext;
        _questionDisplayManager = questionDisplayManager;
        _definitionDisplayManager = definitionDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _bankStore = bankStore;
        _authorizationService = authorizationService;
        _htmlSanitizer = htmlSanitizer;
        _notifier = notifier;
        _localizerFactory = localizerFactory;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the designer.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/design", "SurveysDesign")]
    public async Task<IActionResult> Design(string id)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        UseDraft(survey, survey.Draft);

        return View(await BuildDesignerAsync(survey, await _definitionDisplayManager.BuildEditorAsync(survey.Draft, _updateModelAccessor.ModelUpdater, false)));
    }

    /// <summary>
    /// Saves the survey-wide settings of the draft.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="draftVersion">When the designer loaded the draft, as ticks.</param>
    /// <returns>The page, or a redirect.</returns>
    [HttpPost]
    [ActionName(nameof(Design))]
    [Admin("surveys/{id}/design", "SurveysDesign")]
    public async Task<IActionResult> DesignPost(string id, long draftVersion)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        var draft = SurveyDefinitionCloner.Clone(survey.Draft) ?? new SurveyDefinition();
        UseDraft(survey, draft);

        var editor = await _definitionDisplayManager.UpdateEditorAsync(draft, _updateModelAccessor.ModelUpdater, false);

        if (!ModelState.IsValid)
        {
            return View(nameof(Design), await BuildDesignerAsync(survey, editor));
        }

        var expected = draftVersion > 0 ? new DateTime(draftVersion, DateTimeKind.Utc) : (DateTime?)null;

        if (await _manager.SaveDraftAsync(survey, draft, expected, HttpContext.RequestAborted) == SurveyErrorCode.Busy)
        {
            await _notifier.ErrorAsync(H["Someone else changed the draft since you opened it. Reload the designer and make your changes again."]);

            return RedirectToAction(nameof(Design), new { id });
        }

        await _notifier.SuccessAsync(H["The draft has been saved."]);

        // Save and publish: the draft is saved first, so no edit is lost on the way to the publish page.
        if (Request.Form.ContainsKey("submit.Publish"))
        {
            return RedirectToAction(nameof(Publish), new { id });
        }

        return RedirectToAction(nameof(Design), new { id });
    }

    /// <summary>
    /// Applies an outline command (add, move, duplicate or delete a page or question) and returns the new outline.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="command">The command.</param>
    /// <returns>The outline.</returns>
    [HttpPost]
    [Admin("surveys/{id}/draft/commands", "SurveysDraftCommands")]
    public async Task<IActionResult> Commands(string id, [FromBody] SurveyDraftCommand command)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        if (command is null)
        {
            return BadRequest();
        }

        var draft = SurveyDefinitionCloner.Clone(survey.Draft) ?? new SurveyDefinition();
        var result = _draftEditor.Apply(draft, command);

        if (!result.Succeeded)
        {
            return BadRequest(result.Error);
        }

        if (await _manager.SaveDraftAsync(survey, draft, ExpectedDraftVersion(), HttpContext.RequestAborted) == SurveyErrorCode.Busy)
        {
            return DraftConflict();
        }

        Response.Headers["X-Survey-Item"] = result.ItemId ?? string.Empty;
        Response.Headers["X-Survey-Draft-Version"] = DraftVersion(survey).ToString(CultureInfo.InvariantCulture);

        return PartialView("_SurveyOutline", BuildOutline(survey));
    }

    /// <summary>
    /// Returns the outline of the draft.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>The outline.</returns>
    [Admin("surveys/{id}/draft/outline", "SurveysDraftOutline")]
    public async Task<IActionResult> Outline(string id)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        return PartialView("_SurveyOutline", BuildOutline(survey));
    }

    /// <summary>
    /// Returns the editor of a question.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="questionId">The question.</param>
    /// <returns>The editor.</returns>
    [Admin("surveys/{id}/draft/questions/{questionId}", "SurveysDraftQuestion")]
    public async Task<IActionResult> Question(string id, string questionId)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        var question = SurveyDefinitions.FindQuestion(survey.Draft, questionId);

        if (question is null)
        {
            return NotFound();
        }

        UseDraft(survey, survey.Draft);

        return PartialView("_SurveyQuestionEditor", await BuildQuestionEditorAsync(survey, question, await _questionDisplayManager.BuildEditorAsync(question, _updateModelAccessor.ModelUpdater, false)));
    }

    /// <summary>
    /// Saves a question and returns its editor.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="questionId">The question.</param>
    /// <returns>The editor.</returns>
    [HttpPost]
    [ActionName(nameof(Question))]
    [Admin("surveys/{id}/draft/questions/{questionId}", "SurveysDraftQuestion")]
    public async Task<IActionResult> QuestionPost(string id, string questionId)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        var draft = SurveyDefinitionCloner.Clone(survey.Draft) ?? new SurveyDefinition();
        var question = SurveyDefinitions.FindQuestion(draft, questionId);

        if (question is null)
        {
            return NotFound();
        }

        UseDraft(survey, draft);

        var editor = await _questionDisplayManager.UpdateEditorAsync(question, _updateModelAccessor.ModelUpdater, false);
        var nameTaken = SurveyDefinitions.Questions(draft).Any(item => item.Question.Id != question.Id && string.Equals(item.Question.Name, question.Name, StringComparison.OrdinalIgnoreCase))
            || draft.Variables.Any(variable => string.Equals(variable.Name, question.Name, StringComparison.OrdinalIgnoreCase));

        if (nameTaken)
        {
            ModelState.AddModelError(string.Empty, S["Another question or variable uses the code {0}.", question.Name]);
        }

        var model = await BuildQuestionEditorAsync(survey, question, editor);

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

            return PartialView("_SurveyQuestionEditor", model);
        }

        if (await _manager.SaveDraftAsync(survey, draft, ExpectedDraftVersion(), HttpContext.RequestAborted) == SurveyErrorCode.Busy)
        {
            return DraftConflict();
        }

        model.Saved = true;
        Response.Headers["X-Survey-Draft-Version"] = DraftVersion(survey).ToString(CultureInfo.InvariantCulture);

        return PartialView("_SurveyQuestionEditor", model);
    }

    /// <summary>
    /// Returns the editor of a page.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="pageId">The page.</param>
    /// <returns>The editor.</returns>
    [Admin("surveys/{id}/draft/pages/{pageId}", "SurveysDraftPage")]
    public async Task<IActionResult> Page(string id, string pageId)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        var page = survey.Draft?.Pages.FirstOrDefault(item => item.Id == pageId);

        if (page is null)
        {
            return NotFound();
        }

        return PartialView("_SurveyPageEditor", await BuildPageEditorAsync(survey, survey.Draft, page));
    }

    /// <summary>
    /// Saves a page and returns its editor.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="pageId">The page.</param>
    /// <param name="model">The posted page.</param>
    /// <returns>The editor.</returns>
    [HttpPost]
    [ActionName(nameof(Page))]
    [Admin("surveys/{id}/draft/pages/{pageId}", "SurveysDraftPage")]
    public async Task<IActionResult> PagePost(string id, string pageId, SurveyPageEditorViewModel model)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        var draft = SurveyDefinitionCloner.Clone(survey.Draft) ?? new SurveyDefinition();
        var page = draft.Pages.FirstOrDefault(item => item.Id == pageId);

        if (page is null || model is null)
        {
            return NotFound();
        }

        if (!SurveyNames.IsValidVariableCode(model.Name?.Trim()))
        {
            ModelState.AddModelError(nameof(model.Name), S["The code needs letters, digits and underscores, and must start with a letter."]);
        }
        else if (draft.Pages.Any(other => other.Id != page.Id && string.Equals(other.Name, model.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.Name), S["Another page uses this code."]);
        }

        if (ModelState.IsValid)
        {
            page.Title = model.Title?.Trim();
            page.Name = model.Name.Trim();
            page.Description = string.IsNullOrWhiteSpace(model.Description) ? null : _htmlSanitizer.Sanitize(model.Description);
            page.Visibility = new SurveyVisibility
            {
                Mode = model.VisibilityMode,
                Rule = model.VisibilityMode == VisibilityMode.Always ? null : SurveyRuleJson.Read(model.VisibilityRule),
            };
            page.QuestionOrder = model.QuestionOrder;
            page.AllowBack = model.AllowBack;
            page.TimeLimitSeconds = model.TimeLimitSeconds is > 0 ? model.TimeLimitSeconds : null;
            page.Branches = (model.Branches ?? [])
                .Where(branch => branch is not null && !string.IsNullOrEmpty(branch.TargetId) && branch.TargetId.Contains(':'))
                .Select(branch => new SurveyBranch
                {
                    Id = string.IsNullOrEmpty(branch.Id) ? IdGenerator.GenerateId() : branch.Id,
                    Rule = SurveyRuleJson.Read(branch.Rule),
                    Formula = string.IsNullOrWhiteSpace(branch.Formula) ? null : branch.Formula.Trim(),
                    TargetKind = branch.TargetId.StartsWith("ending:", StringComparison.Ordinal) ? BranchTargetKind.Ending : BranchTargetKind.Page,
                    TargetId = branch.TargetId.Substring(branch.TargetId.IndexOf(':') + 1),
                })
                .ToList();

            var pools = new List<QuestionPool>();

            // Without ViewQuestionBanks the bank fields are not shown, so the pool keeps the bank draws it already has
            // and the user can't attach a bank. With it, only a bank that exists may be attached.
            var bankIds = await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ViewQuestionBanks)
                ? (await _bankStore.GetAllAsync()).Select(bank => bank.ItemId).ToHashSet(StringComparer.Ordinal)
                : null;

            foreach (var pool in (model.Pools ?? []).Where(pool => pool is not null && !string.IsNullOrWhiteSpace(pool.Title)))
            {
                var existing = page.Pools.FirstOrDefault(item => item.Id == pool.Id);

                if (bankIds is null)
                {
                    pools.Add(new QuestionPool
                    {
                        Id = string.IsNullOrEmpty(pool.Id) ? IdGenerator.GenerateId() : pool.Id,
                        Name = SurveyNames.IsValidVariableCode(pool.Name?.Trim()) ? pool.Name.Trim() : $"pool_{pools.Count + 1}",
                        Title = pool.Title.Trim(),
                        DrawCount = Math.Max(0, pool.DrawCount),
                        Shuffle = pool.Shuffle,
                        EvenlyPresent = pool.EvenlyPresent,
                        BankDraws = existing?.BankDraws ?? [],
                    });

                    continue;
                }

                if (!string.IsNullOrEmpty(pool.BankId) && !bankIds.Contains(pool.BankId))
                {
                    pool.BankId = null;
                }

                pools.Add(new QuestionPool
                {
                    Id = string.IsNullOrEmpty(pool.Id) ? IdGenerator.GenerateId() : pool.Id,
                    Name = SurveyNames.IsValidVariableCode(pool.Name?.Trim()) ? pool.Name.Trim() : $"pool_{pools.Count + 1}",
                    Title = pool.Title.Trim(),
                    DrawCount = Math.Max(0, pool.DrawCount),
                    Shuffle = pool.Shuffle,
                    EvenlyPresent = pool.EvenlyPresent,
                    BankDraws = string.IsNullOrEmpty(pool.BankId)
                        ? []
                        :
                        [
                            new BankDraw
                            {
                                BankId = pool.BankId,
                                Tags = (pool.BankTags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                                Difficulty = pool.BankDifficulty is >= 1 and <= 5 ? pool.BankDifficulty : null,
                                MaxCandidates = pool.BankMaxCandidates is > 0 ? Math.Min(pool.BankMaxCandidates.Value, SurveyBankDrawResolver.HardMaxCandidates) : null,
                            },
                            .. (existing?.BankDraws ?? []).Skip(1),
                        ],
                });
            }

            var poolIds = pools.Select(pool => pool.Id).ToHashSet(StringComparer.Ordinal);

            foreach (var question in page.Questions.Where(question => question.PoolId is not null && !poolIds.Contains(question.PoolId)))
            {
                question.PoolId = null;
            }

            page.Pools = pools;

            if (await _manager.SaveDraftAsync(survey, draft, ExpectedDraftVersion(), HttpContext.RequestAborted) == SurveyErrorCode.Busy)
            {
                return DraftConflict();
            }

            Response.Headers["X-Survey-Draft-Version"] = DraftVersion(survey).ToString(CultureInfo.InvariantCulture);

            var saved = await BuildPageEditorAsync(survey, draft, page);
            saved.Saved = true;

            return PartialView("_SurveyPageEditor", saved);
        }

        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

        var rebuilt = await BuildPageEditorAsync(survey, draft, page);
        rebuilt.Title = model.Title;
        rebuilt.Name = model.Name;

        return PartialView("_SurveyPageEditor", rebuilt);
    }

    /// <summary>
    /// Replaces the draft with the published version.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="draft">The stamp (ticks) of the draft the designer was opened with; a draft changed since is kept.</param>
    /// <returns>A redirect to the designer.</returns>
    [HttpPost]
    [Admin("surveys/{id}/draft/discard", "SurveysDraftDiscard")]
    public async Task<IActionResult> Discard(string id, long? draft)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(survey.PublishedVersionId))
        {
            await _notifier.WarningAsync(H["The survey has never been published, so there is no version to go back to."]);
        }
        else if (await _publisher.RestoreAsync(id, survey.PublishedVersionId, SurveyDraftStamp.ToUtc(draft), HttpContext.RequestAborted) is var error && error == SurveyErrorCode.Busy)
        {
            await _notifier.ErrorAsync(H["Someone else changed this survey. Reload the page and try again."]);
        }
        else if (error != SurveyErrorCode.None)
        {
            // Nothing was replaced (the published version can't be found), so the draft keeps its unpublished changes.
            await _notifier.ErrorAsync(H["The published version could not be found, so the draft was not replaced."]);
        }
        else
        {
            var restored = await _manager.FindByIdAsync(id);

            if (restored is not null)
            {
                restored.HasUnpublishedChanges = false;
                await _manager.UpdateAsync(restored, null, HttpContext.RequestAborted);
            }

            await _notifier.SuccessAsync(H["The draft has been replaced with the published version."]);
        }

        return RedirectToAction(nameof(Design), new { id });
    }

    /// <summary>
    /// Shows the problems and changes of the draft before it is published.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/publish", "SurveysPublish")]
    public async Task<IActionResult> Publish(string id)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Publish);

        if (survey is null)
        {
            return Forbid();
        }

        return View(new SurveyPublishViewModel
        {
            Survey = survey,
            Preview = await _publisher.PreviewAsync(survey, HttpContext.RequestAborted),
        });
    }

    /// <summary>
    /// Publishes the draft.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="model">The form.</param>
    /// <returns>The page, or a redirect.</returns>
    [HttpPost]
    [ActionName(nameof(Publish))]
    [Admin("surveys/{id}/publish", "SurveysPublish")]
    public async Task<IActionResult> PublishPost(string id, SurveyPublishViewModel model)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Publish);

        if (survey is null)
        {
            return Forbid();
        }

        var preview = await _publisher.PreviewAsync(survey, HttpContext.RequestAborted);

        if (preview.Diff.HasDestructiveChanges && !string.IsNullOrEmpty(survey.PublishedVersionId) && model?.ConfirmDestructive != true)
        {
            ModelState.AddModelError(nameof(model.ConfirmDestructive), S["Confirm that you want to publish changes that affect existing answers."]);

            return View(new SurveyPublishViewModel { Survey = survey, Preview = preview, Notes = model?.Notes });
        }

        var result = await _publisher.PublishAsync(id, model?.Notes?.Trim(), User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["The draft has problems to fix before it can be published."]);

            return View(new SurveyPublishViewModel { Survey = survey, Preview = await _publisher.PreviewAsync(survey, HttpContext.RequestAborted), Notes = model?.Notes });
        }

        await _notifier.SuccessAsync(H["Version {0} has been published.", result.Version.Number]);

        return RedirectToAction("Index", "SurveyCollectors", new { id });
    }

    private async Task<Survey> AuthorizeAsync(string id, SurveyOperation operation)
    {
        var survey = string.IsNullOrEmpty(id) ? null : await _manager.FindByIdAsync(id);

        if (survey is null || !await _surveyAuthorization.AuthorizeAsync(User, survey, operation, HttpContext.RequestAborted))
        {
            return null;
        }

        survey.Draft ??= new SurveyDefinition();

        return survey;
    }

    private void UseDraft(Survey survey, SurveyDefinition definition)
    {
        _designerContext.Survey = survey;
        _designerContext.Definition = definition;
    }

    private Task<SurveyDesignerViewModel> BuildDesignerAsync(Survey survey, IShape settingsEditor)
    {
        var picker = _registry.GetControls()
            .Select(descriptor => new SurveyControlPickerItem
            {
                ControlType = descriptor.Name,
                DisplayName = Localized(descriptor.DisplayName) ?? descriptor.Name,
                Description = Localized(descriptor.Description),
                Icon = descriptor.Icon,
                Category = descriptor.Category,
            })
            .Concat(_registry.GetPresets().Select(preset => new SurveyControlPickerItem
            {
                ControlType = preset.BaseControl,
                Preset = preset.Name,
                DisplayName = Localized(preset.DisplayName) ?? preset.Name,
                Description = Localized(preset.Description),
                Icon = preset.Icon,
                Category = preset.Category ?? _registry.GetControl(preset.BaseControl)?.Category,
            }))
            .OrderBy(item => SurveyControlCategories.Order(item.Category))
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCulture)
            .ToList();

        return Task.FromResult(new SurveyDesignerViewModel
        {
            Survey = survey,
            Outline = BuildOutline(survey),
            SettingsEditor = settingsEditor,
            Picker = picker,
            DraftVersion = DraftVersion(survey),
        });
    }

    private SurveyOutlineViewModel BuildOutline(Survey survey)
    {
        var controls = _registry.GetControls().ToDictionary(descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase);

        return new SurveyOutlineViewModel
        {
            Survey = survey,
            Controls = controls,
            ControlNames = controls.ToDictionary(pair => pair.Key, pair => Localized(pair.Value.DisplayName) ?? pair.Key, StringComparer.OrdinalIgnoreCase),
        };
    }

    private Task<SurveyQuestionEditorViewModel> BuildQuestionEditorAsync(Survey survey, SurveyQuestion question, IShape editor)
    {
        var descriptor = _registry.GetControl(question.ControlType);

        return Task.FromResult(new SurveyQuestionEditorViewModel
        {
            SurveyId = survey.ItemId,
            QuestionId = question.Id,
            ControlName = Localized(descriptor?.DisplayName) ?? question.ControlType,
            Editor = editor,
            Unavailable = descriptor is null,
        });
    }

    private async Task<SurveyPageEditorViewModel> BuildPageEditorAsync(Survey survey, SurveyDefinition definition, SurveyPage page)
    {
        var pageIndex = definition.Pages.IndexOf(page);
        var canUseBanks = await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ViewQuestionBanks);
        IEnumerable<QuestionBank> banks = canUseBanks ? await _bankStore.GetAllAsync() : [];
        var targets = definition.Pages
            .Skip(pageIndex + 1)
            .Select((later, index) => new SelectListItem(S["Page: {0}", string.IsNullOrWhiteSpace(later.Title) ? (pageIndex + index + 2).ToString(CultureInfo.CurrentCulture) : later.Title], "page:" + later.Id))
            .Concat(definition.Endings.Select(ending => new SelectListItem(S["Ending: {0}", string.IsNullOrWhiteSpace(ending.Title) ? ending.Name : ending.Title], "ending:" + ending.Id)))
            .ToList();

        return new SurveyPageEditorViewModel
        {
            SurveyId = survey.ItemId,
            PageId = page.Id,
            Title = page.Title,
            Name = page.Name,
            Description = page.Description,
            VisibilityMode = page.Visibility?.Mode ?? VisibilityMode.Always,
            VisibilityRule = SurveyRuleJson.Write(page.Visibility?.Rule),
            QuestionOrder = page.QuestionOrder,
            AllowBack = page.AllowBack,
            TimeLimitSeconds = page.TimeLimitSeconds,
            Branches = page.Branches.Select(branch => new SurveyBranchEditViewModel
            {
                Id = branch.Id,
                Rule = SurveyRuleJson.Write(branch.Rule),
                Formula = branch.Formula,
                TargetKind = branch.TargetKind,
                TargetId = (branch.TargetKind == BranchTargetKind.Ending ? "ending:" : "page:") + branch.TargetId,
            }).ToList(),
            Pools = page.Pools.Select(pool => new SurveyPoolEditViewModel
            {
                Id = pool.Id,
                Name = pool.Name,
                Title = pool.Title,
                DrawCount = pool.DrawCount,
                Shuffle = pool.Shuffle,
                EvenlyPresent = pool.EvenlyPresent,
                BankId = pool.BankDraws.FirstOrDefault()?.BankId,
                BankTags = string.Join(", ", pool.BankDraws.FirstOrDefault()?.Tags ?? []),
                BankDifficulty = pool.BankDraws.FirstOrDefault()?.Difficulty,
                BankMaxCandidates = pool.BankDraws.FirstOrDefault()?.MaxCandidates,
            }).ToList(),
            PageRuleSources = SurveyRuleSources.Build(definition, _registry, pageIndex, excludeLastPage: true),
            BranchRuleSources = SurveyRuleSources.Build(definition, _registry, pageIndex),
            Targets = targets,
            CanUseBanks = canUseBanks,
            Banks = banks.OrderBy(bank => bank.Title).Select(bank => new SelectListItem(bank.Title ?? bank.Name, bank.ItemId)).ToList(),
        };
    }

    // The draft version the designer page last saw, sent with every panel save and outline command. The page and question
    // forms post their whole state (choices, branches, pools), so a save over a draft another author changed since would
    // overwrite that change.
    private DateTime? ExpectedDraftVersion()
        => long.TryParse(Request.Headers["X-Survey-Draft-Version"].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            ? SurveyDraftStamp.ToUtc(ticks)
            : null;

    private ContentResult DraftConflict()
        => new()
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentType = "text/plain; charset=utf-8",
            Content = S["Someone else changed the draft since you opened the designer. Reload the page and make your change again."],
        };

    private static long DraftVersion(Survey survey)
        => SurveyDraftStamp.From(survey.DraftModifiedUtc) ?? 0;

    private string Localized(LocalizationSource source)
        => source is null ? null : _localizerFactory.Localize(source).Value;
}
