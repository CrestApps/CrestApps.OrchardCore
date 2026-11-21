using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The admin pages of question banks: the banks, the questions of a bank, the question editor and the import.
/// </summary>
public sealed class QuestionBanksController : Controller
{
    private readonly IQuestionBankManager _manager;
    private readonly IQuestionBankQueries _queries;
    private readonly QuestionBankStore _banks;
    private readonly BankQuestionStore _questions;
    private readonly ISurveyControlRegistry _controls;
    private readonly IEnumerable<ISurveyImportFormat> _formats;
    private readonly IDisplayManager<SurveyQuestion> _questionDisplayManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;
    private readonly INotifier _notifier;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly PagerOptions _pagerOptions;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionBanksController"/> class.
    /// </summary>
    /// <param name="manager">The bank manager.</param>
    /// <param name="queries">The bank queries.</param>
    /// <param name="banks">The bank store.</param>
    /// <param name="questions">The bank question store.</param>
    /// <param name="controls">The question type registry.</param>
    /// <param name="formats">The import formats.</param>
    /// <param name="questionDisplayManager">The question editor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="updateModelAccessor">The model updater accessor.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="localizerFactory">The localizer factory.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public QuestionBanksController(
        IQuestionBankManager manager,
        IQuestionBankQueries queries,
        QuestionBankStore banks,
        BankQuestionStore questions,
        ISurveyControlRegistry controls,
        IEnumerable<ISurveyImportFormat> formats,
        IDisplayManager<SurveyQuestion> questionDisplayManager,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        ISiteService siteService,
        INotifier notifier,
        IUpdateModelAccessor updateModelAccessor,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizerFactory localizerFactory,
        IHtmlLocalizer<QuestionBanksController> htmlLocalizer,
        IStringLocalizer<QuestionBanksController> stringLocalizer)
    {
        _manager = manager;
        _queries = queries;
        _banks = banks;
        _questions = questions;
        _controls = controls;
        _formats = formats;
        _questionDisplayManager = questionDisplayManager;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
        _notifier = notifier;
        _updateModelAccessor = updateModelAccessor;
        _pagerOptions = pagerOptions.Value;
        _localizerFactory = localizerFactory;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the question banks.
    /// </summary>
    /// <param name="search">The search text.</param>
    /// <param name="pagerParameters">The pager.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/banks", "SurveysQuestionBanks")]
    public async Task<IActionResult> Index(string search, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ViewQuestionBanks))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, _pagerOptions.GetPageSize());
        var (count, items) = await _queries.ListBanksAsync(search, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(search))
        {
            routeData.Values.Add("search", search);
        }

        var model = new QuestionBankIndexViewModel
        {
            Search = search,
            CanManage = await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks),
            List = new AdminListContext<QuestionBank>
            {
                Source = items,
                Pager = await _shapeFactory.PagerAsync(pager, count, routeData),
            },
        };

        return View(model);
    }

    /// <summary>
    /// Applies the search of the banks list.
    /// </summary>
    /// <param name="model">The posted list.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("surveys/banks", "SurveysQuestionBanks")]
    public IActionResult IndexFilter(QuestionBankIndexViewModel model)
        => RedirectToAction(nameof(Index), new { search = model?.Search });

    /// <summary>
    /// Shows the form of a new bank.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("surveys/banks/create", "SurveysQuestionBanksCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        return View("Edit", new QuestionBankEditViewModel());
    }

    /// <summary>
    /// Creates a bank.
    /// </summary>
    /// <param name="model">The posted form.</param>
    /// <returns>A redirect to the new bank, or the form with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("surveys/banks/create", "SurveysQuestionBanksCreate")]
    public async Task<IActionResult> CreatePost(QuestionBankEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var bank = await _manager.NewBankAsync(HttpContext.RequestAborted);
        Apply(bank, model);

        var result = await _manager.SaveBankAsync(bank, isNew: true, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);

            return View("Edit", model);
        }

        await _notifier.SuccessAsync(H["The question bank was created."]);

        return RedirectToAction(nameof(Bank), new { id = bank.ItemId });
    }

    /// <summary>
    /// Shows the form of a bank.
    /// </summary>
    /// <param name="id">The bank.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/banks/{id}/edit", "SurveysQuestionBanksEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var bank = await _banks.FindByIdAsync(id, HttpContext.RequestAborted);

        if (bank is null)
        {
            return NotFound();
        }

        return View(new QuestionBankEditViewModel
        {
            Id = bank.ItemId,
            Name = bank.Name,
            Title = bank.Title,
            Description = bank.Description,
            Tags = string.Join(", ", bank.Tags ?? []),
        });
    }

    /// <summary>
    /// Saves a bank.
    /// </summary>
    /// <param name="id">The bank.</param>
    /// <param name="model">The posted form.</param>
    /// <returns>A redirect to the bank, or the form with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("surveys/banks/{id}/edit", "SurveysQuestionBanksEdit")]
    public async Task<IActionResult> EditPost(string id, QuestionBankEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var bank = await _banks.FindByIdAsync(id, HttpContext.RequestAborted);

        if (bank is null)
        {
            return NotFound();
        }

        model.Id = bank.ItemId;
        Apply(bank, model);

        var result = await _manager.SaveBankAsync(bank, isNew: false, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);

            return View(model);
        }

        await _notifier.SuccessAsync(H["The question bank was saved."]);

        return RedirectToAction(nameof(Bank), new { id = bank.ItemId });
    }

    /// <summary>
    /// Deletes a bank and its questions.
    /// </summary>
    /// <param name="id">The bank.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("surveys/banks/{id}/delete", "SurveysQuestionBanksDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        if (await _manager.DeleteBankAsync(id, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The question bank was deleted. Published surveys keep the questions they drew from it."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the questions of a bank.
    /// </summary>
    /// <param name="id">The bank.</param>
    /// <param name="options">The filters.</param>
    /// <param name="pagerParameters">The pager.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/banks/{id}", "SurveysQuestionBank")]
    public async Task<IActionResult> Bank(string id, BankQuestionFilterOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ViewQuestionBanks))
        {
            return Forbid();
        }

        var bank = await _banks.FindByIdAsync(id, HttpContext.RequestAborted);

        if (bank is null)
        {
            return NotFound();
        }

        options ??= new BankQuestionFilterOptions();

        var pager = new Pager(pagerParameters, _pagerOptions.GetPageSize());
        var query = new BankQuestionQuery
        {
            BankId = bank.ItemId,
            ControlType = string.IsNullOrEmpty(options.Type) ? null : options.Type,
            Tag = string.IsNullOrEmpty(options.Tag) ? null : options.Tag,
            Difficulty = int.TryParse(options.Difficulty, out var difficulty) ? difficulty : null,
            Retired = options.Show switch
            {
                "retired" => true,
                "all" => null,
                _ => false,
            },
            Search = options.Search,
            Skip = pager.GetStartIndex(),
            Take = pager.PageSize,
        };

        var (count, items) = await _queries.ListQuestionsAsync(query, HttpContext.RequestAborted);
        var routeData = new RouteData();
        AddRouteValue(routeData, "options.Search", options.Search);
        AddRouteValue(routeData, "options.Type", options.Type);
        AddRouteValue(routeData, "options.Tag", options.Tag);
        AddRouteValue(routeData, "options.Difficulty", options.Difficulty);
        AddRouteValue(routeData, "options.Show", options.Show);

        var canManage = await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks);
        var controls = _controls.GetControls();

        await FillOptionsAsync(options, bank.ItemId, controls, canManage);

        var (_, allBanks) = await _queries.ListBanksAsync(null, 0, SurveysConstants.PageSize, HttpContext.RequestAborted);

        var model = new QuestionBankViewModel
        {
            Bank = bank,
            Options = options,
            CanManage = canManage,
            Controls = controls
                .Where(control => control.Has(SurveyControlCapabilities.Answerable))
                .Select(control => (control.Name, DisplayNameOf(control), control.Category, control.Icon))
                .ToList(),
            OtherBanks = allBanks.Where(item => item.ItemId != bank.ItemId).ToList(),
            List = new AdminListContext<BankQuestion>
            {
                Source = items,
                Pager = await _shapeFactory.PagerAsync(pager, count, routeData),
                ToolbarActions = await _shapeFactory.CreateAsync<BankQuestionFilterOptions>("BankQuestionsAdminListFilters", filters =>
                {
                    filters.Type = options.Type;
                    filters.Tag = options.Tag;
                    filters.Difficulty = options.Difficulty;
                    filters.Show = options.Show;
                    filters.Types = options.Types;
                    filters.Tags = options.Tags;
                    filters.Difficulties = options.Difficulties;
                    filters.Shows = options.Shows;
                }),
            },
        };

        return View(model);
    }

    /// <summary>
    /// Applies the filters or the bulk action of the questions of a bank.
    /// </summary>
    /// <param name="id">The bank.</param>
    /// <param name="options">The posted filters and bulk action.</param>
    /// <param name="itemIds">The selected questions.</param>
    /// <returns>A redirect to the bank.</returns>
    [HttpPost]
    [ActionName(nameof(Bank))]
    [Admin("surveys/banks/{id}", "SurveysQuestionBank")]
    public async Task<IActionResult> BankPost(string id, BankQuestionFilterOptions options, IEnumerable<string> itemIds)
    {
        options ??= new BankQuestionFilterOptions();

        if (Request.Form.ContainsKey("submit.BulkAction"))
        {
            if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
            {
                return Forbid();
            }

            var ids = (itemIds ?? []).ToArray();
            var changed = options.BulkAction switch
            {
                "retire" => await _manager.RetireQuestionsAsync(ids, retired: true, HttpContext.RequestAborted),
                "restore" => await _manager.RetireQuestionsAsync(ids, retired: false, HttpContext.RequestAborted),
                "move" => await _manager.MoveQuestionsAsync(ids, options.TargetBankId, HttpContext.RequestAborted),
                "delete" => await _manager.DeleteQuestionsAsync(ids, HttpContext.RequestAborted),
                _ => 0,
            };

            await _notifier.SuccessAsync(H["{0} questions changed.", changed]);
        }

        return RedirectToAction(nameof(Bank), new
        {
            id,
            options.Search,
            options.Type,
            options.Tag,
            options.Difficulty,
            options.Show,
        });
    }

    /// <summary>
    /// Shows the editor of a new question of a bank.
    /// </summary>
    /// <param name="id">The bank.</param>
    /// <param name="type">The question type or preset.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/banks/{id}/questions/create", "SurveysBankQuestionCreate")]
    public async Task<IActionResult> CreateQuestion(string id, string type)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var bank = await _banks.FindByIdAsync(id, HttpContext.RequestAborted);
        var question = bank is null ? null : await NewQuestionAsync(bank.ItemId, type);

        if (question is null)
        {
            return NotFound();
        }

        return View("EditQuestion", await BuildQuestionModelAsync(bank, question, isNew: true));
    }

    /// <summary>
    /// Creates a question in a bank.
    /// </summary>
    /// <param name="id">The bank.</param>
    /// <param name="type">The question type or preset.</param>
    /// <param name="model">The posted bank fields.</param>
    /// <returns>A redirect to the bank, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(CreateQuestion))]
    [Admin("surveys/banks/{id}/questions/create", "SurveysBankQuestionCreate")]
    public async Task<IActionResult> CreateQuestionPost(string id, string type, [Bind(Prefix = "Bank")] BankQuestionEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var bank = await _banks.FindByIdAsync(id, HttpContext.RequestAborted);
        var question = bank is null ? null : await NewQuestionAsync(bank.ItemId, type);

        if (question is null)
        {
            return NotFound();
        }

        return await SaveQuestionAsync(bank, question, model, isNew: true);
    }

    /// <summary>
    /// Shows the editor of a bank question.
    /// </summary>
    /// <param name="questionId">The question.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/banks/questions/{questionId}", "SurveysBankQuestionEdit")]
    public async Task<IActionResult> EditQuestion(string questionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var question = await _questions.FindByIdAsync(questionId, HttpContext.RequestAborted);
        var bank = question is null ? null : await _banks.FindByIdAsync(question.BankId, HttpContext.RequestAborted);

        if (bank is null)
        {
            return NotFound();
        }

        return View(await BuildQuestionModelAsync(bank, question, isNew: false));
    }

    /// <summary>
    /// Saves a bank question.
    /// </summary>
    /// <param name="questionId">The question.</param>
    /// <param name="model">The posted bank fields.</param>
    /// <returns>A redirect to the bank, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(EditQuestion))]
    [Admin("surveys/banks/questions/{questionId}", "SurveysBankQuestionEdit")]
    public async Task<IActionResult> EditQuestionPost(string questionId, [Bind(Prefix = "Bank")] BankQuestionEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var question = await _questions.FindByIdAsync(questionId, HttpContext.RequestAborted);
        var bank = question is null ? null : await _banks.FindByIdAsync(question.BankId, HttpContext.RequestAborted);

        if (bank is null)
        {
            return NotFound();
        }

        return await SaveQuestionAsync(bank, question, model, isNew: false);
    }

    /// <summary>
    /// Deletes a bank question.
    /// </summary>
    /// <param name="questionId">The question.</param>
    /// <returns>A redirect to its bank.</returns>
    [HttpPost]
    [Admin("surveys/banks/questions/{questionId}/delete", "SurveysBankQuestionDelete")]
    public async Task<IActionResult> DeleteQuestion(string questionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var question = await _questions.FindByIdAsync(questionId, HttpContext.RequestAborted);

        if (question is null)
        {
            return NotFound();
        }

        await _manager.DeleteQuestionsAsync([question.ItemId], HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The question was deleted."]);

        return RedirectToAction(nameof(Bank), new { id = question.BankId });
    }

    /// <summary>
    /// Shows the import page of a bank.
    /// </summary>
    /// <param name="id">The bank.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/banks/{id}/import", "SurveysQuestionBankImport")]
    public async Task<IActionResult> Import(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var bank = await _banks.FindByIdAsync(id, HttpContext.RequestAborted);

        if (bank is null)
        {
            return NotFound();
        }

        return View(ImportModel(bank, new QuestionBankImportViewModel()));
    }

    /// <summary>
    /// Checks a file, or imports its questions into a bank.
    /// </summary>
    /// <param name="id">The bank.</param>
    /// <param name="model">The posted file and format.</param>
    /// <returns>The page with what was found or imported.</returns>
    [HttpPost]
    [ActionName(nameof(Import))]
    [Admin("surveys/banks/{id}/import", "SurveysQuestionBankImport")]
    public async Task<IActionResult> ImportPost(string id, QuestionBankImportViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageQuestionBanks))
        {
            return Forbid();
        }

        var bank = await _banks.FindByIdAsync(id, HttpContext.RequestAborted);

        if (bank is null)
        {
            return NotFound();
        }

        model = ImportModel(bank, model ?? new QuestionBankImportViewModel());

        var format = _formats.FirstOrDefault(item => string.Equals(item.Name, model.Format, StringComparison.OrdinalIgnoreCase));

        if (model.File is null || model.File.Length == 0)
        {
            ModelState.AddModelError(nameof(model.File), S["Choose a file to import."]);

            return View(model);
        }

        if (format is null || !format.Extensions.Contains(Path.GetExtension(model.File.FileName), StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Format), S["The file is not of the chosen format."]);

            return View(model);
        }

        SurveyImportResult read;

        await using (var stream = model.File.OpenReadStream())
        {
            read = await format.ReadAsync(model.File.FileName, stream, HttpContext.RequestAborted);
        }

        model.ReadCount = read.Questions.Count;
        model.Errors.AddRange(read.Errors);

        if (Request.Form.ContainsKey("submit.Check"))
        {
            model.Checked = true;

            return View(model);
        }

        var summary = await _manager.ImportAsync(bank.ItemId, read.Questions, HttpContext.RequestAborted);
        model.Created = summary.Created;
        model.Updated = summary.Updated;
        model.Errors.AddRange(summary.Errors);

        await _notifier.SuccessAsync(H["{0} questions added and {1} updated.", summary.Created, summary.Updated]);

        return View(model);
    }

    private async Task<BankQuestion> NewQuestionAsync(string bankId, string type)
    {
        var preset = _controls.GetPreset(type);
        var control = _controls.GetControl(preset?.BaseControl ?? type);

        if (control is null)
        {
            return null;
        }

        var question = await _manager.NewQuestionAsync(bankId, control.Name, HttpContext.RequestAborted);
        preset?.Initialize?.Invoke(question.Question);

        return question;
    }

    private async Task<IActionResult> SaveQuestionAsync(QuestionBank bank, BankQuestion question, BankQuestionEditViewModel model, bool isNew)
    {
        model ??= new BankQuestionEditViewModel();

        var editor = await _questionDisplayManager.UpdateEditorAsync(question.Question, _updateModelAccessor.ModelUpdater, isNew, string.Empty, string.Empty);

        question.Tags = SplitTags(model.Tags);
        question.Difficulty = model.Difficulty;
        question.Retired = model.Retired;

        if (ModelState.IsValid)
        {
            var result = await _manager.SaveQuestionAsync(question, isNew, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The question was saved."]);

                return RedirectToAction(nameof(Bank), new { id = bank.ItemId });
            }

            AddErrors(result.Errors);
        }

        var page = await BuildQuestionModelAsync(bank, question, isNew, editor);
        page.Tags = model.Tags;

        return View("EditQuestion", page);
    }

    private async Task<BankQuestionEditViewModel> BuildQuestionModelAsync(QuestionBank bank, BankQuestion question, bool isNew, IShape editor = null)
    {
        var control = _controls.GetControl(question.Question?.ControlType);

        return new BankQuestionEditViewModel
        {
            BankId = bank.ItemId,
            BankTitle = bank.Title,
            QuestionId = isNew ? null : question.ItemId,
            ControlType = question.Question?.ControlType,
            ControlTypeName = DisplayNameOf(control) ?? question.Question?.ControlType,
            Editor = editor ?? await _questionDisplayManager.BuildEditorAsync(question.Question, _updateModelAccessor.ModelUpdater, isNew, string.Empty, string.Empty),
            Tags = string.Join(", ", question.Tags ?? []),
            Difficulty = question.Difficulty,
            Retired = question.Retired,
            Revision = question.Revision,
            ModifiedUtc = question.ModifiedUtc,
        };
    }

    private QuestionBankImportViewModel ImportModel(QuestionBank bank, QuestionBankImportViewModel model)
    {
        model.BankId = bank.ItemId;
        model.BankTitle = bank.Title;
        model.Formats = _formats
            .OrderBy(format => format.DisplayName, StringComparer.CurrentCulture)
            .Select(format => new SelectListItem(format.DisplayName + " (" + string.Join(", ", format.Extensions) + ")", format.Name, string.Equals(format.Name, model.Format, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return model;
    }

    private async Task FillOptionsAsync(BankQuestionFilterOptions options, string bankId, IReadOnlyList<SurveyControlDescriptor> controls, bool canManage)
    {
        options.Types = [new SelectListItem(S["All types"], string.Empty, string.IsNullOrEmpty(options.Type))];
        options.Types.AddRange(controls.Select(control => new SelectListItem(DisplayNameOf(control), control.Name, control.Name == options.Type)));

        options.Tags = [new SelectListItem(S["All tags"], string.Empty, string.IsNullOrEmpty(options.Tag))];
        options.Tags.AddRange((await _queries.ListTagsAsync(bankId, HttpContext.RequestAborted)).Select(tag => new SelectListItem(tag, tag, tag == options.Tag)));

        options.Difficulties =
        [
            new SelectListItem(S["Any difficulty"], string.Empty, string.IsNullOrEmpty(options.Difficulty)),
            new SelectListItem(S["1 - Easiest"], "1", options.Difficulty == "1"),
            new SelectListItem(S["2 - Easy"], "2", options.Difficulty == "2"),
            new SelectListItem(S["3 - Medium"], "3", options.Difficulty == "3"),
            new SelectListItem(S["4 - Hard"], "4", options.Difficulty == "4"),
            new SelectListItem(S["5 - Hardest"], "5", options.Difficulty == "5"),
        ];

        options.Shows =
        [
            new SelectListItem(S["Active questions"], string.Empty, string.IsNullOrEmpty(options.Show)),
            new SelectListItem(S["Retired questions"], "retired", options.Show == "retired"),
            new SelectListItem(S["All questions"], "all", options.Show == "all"),
        ];

        options.BulkActions = canManage
            ?
            [
                new SelectListItem(S["Retire"], "retire"),
                new SelectListItem(S["Restore"], "restore"),
                new SelectListItem(S["Move to another bank"], "move"),
                new SelectListItem(S["Delete"], "delete"),
            ]
            : [];
    }

    private static void Apply(QuestionBank bank, QuestionBankEditViewModel model)
    {
        bank.Name = model?.Name;
        bank.Title = model?.Title;
        bank.Description = model?.Description;
        bank.Tags = SplitTags(model?.Tags);
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    private string DisplayNameOf(SurveyControlDescriptor control)
        => control is null
            ? null
            : control.DisplayName is null ? control.Name : _localizerFactory.Localize(control.DisplayName).Value;

    private static List<string> SplitTags(string tags)
        => string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static void AddRouteValue(RouteData routeData, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            routeData.Values.Add(key, value);
        }
    }
}
