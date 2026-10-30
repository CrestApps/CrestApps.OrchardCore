using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// The AI test case catalog: simulation tests of AI profiles, their runs, and tests made from real conversations.
/// </summary>
[Admin]
[Feature(AIConstants.Feature.Quality)]
public sealed class AITestCasesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAITestCaseStore _store;
    private readonly IAITestRunStore _runStore;
    private readonly AITestRunner _runner;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AITestCase> _displayManager;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AITestCasesController"/> class.
    /// </summary>
    /// <param name="store">The test case store.</param>
    /// <param name="runStore">The test run store.</param>
    /// <param name="runner">Queues the runs.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The test case display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AITestCasesController(
        IAITestCaseStore store,
        IAITestRunStore runStore,
        AITestRunner runner,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AITestCase> displayManager,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<AITestCasesController> htmlLocalizer,
        IStringLocalizer<AITestCasesController> stringLocalizer)
    {
        _store = store;
        _runStore = runStore;
        _runner = runner;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the test cases.
    /// </summary>
    /// <param name="options">The search.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("ai/tests", "AITestCasesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions<AITestBulkAction> options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await _store.PageAsync(pager.Page, pager.PageSize, new QueryContext { Name = options.Search });
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var pagerShape = await shapeFactory.PagerAsync(pager, result.Count, routeData);

        options.BulkActions =
        [
            new SelectListItem(S["Run"], nameof(AITestBulkAction.Run)),
            new SelectListItem(S["Run 5 times"], nameof(AITestBulkAction.RunFiveTimes)),
            new SelectListItem(S["Delete"], nameof(AITestBulkAction.Delete)),
        ];

        return View(new AITestCasesIndexViewModel
        {
            Options = options,
            HasTests = result.Count > 0 || !string.IsNullOrEmpty(options.Search),
            List = new AdminListContext<AITestCase>
            {
                Source = result.Entries,
                Pager = pagerShape,
            },
        });
    }

    /// <summary>
    /// Applies the search.
    /// </summary>
    /// <param name="model">The submitted list.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/tests", "AITestCasesIndex")]
    public async Task<IActionResult> IndexFilterPost(AITestCasesIndexViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
            { "pageSize", pagerParameters.PageSize },
        });
    }

    /// <summary>
    /// Runs or deletes the selected tests.
    /// </summary>
    /// <param name="options">The chosen bulk action.</param>
    /// <param name="itemIds">The selected tests.</param>
    /// <returns>A redirect to the runs, or to the list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/tests", "AITestCasesIndex")]
    public async Task<IActionResult> IndexPost(CatalogEntryOptions<AITestBulkAction> options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var ids = itemIds?.Where(id => !string.IsNullOrEmpty(id)).ToArray() ?? [];

        if (ids.Length == 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var testCases = await _store.GetAsync(ids);

        if (options.BulkAction == AITestBulkAction.Delete)
        {
            foreach (var testCase in testCases)
            {
                await _store.DeleteAsync(testCase);
            }

            await _notifier.SuccessAsync(H["{0} test case(s) deleted.", testCases.Count]);

            return RedirectToAction(nameof(Index));
        }

        var repeats = options.BulkAction == AITestBulkAction.RunFiveTimes ? 5 : 1;

        return await QueueAndShowAsync(testCases, repeats);
    }

    /// <summary>
    /// Shows the form for a new test case.
    /// </summary>
    /// <returns>The form.</returns>
    [Admin("ai/tests/create", "AITestCasesCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var testCase = new AITestCase();

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New test case"],
            Editor = await _displayManager.BuildEditorAsync(testCase, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Saves a new test case.
    /// </summary>
    /// <param name="sourceSessionId">The conversation the test was made from, if any.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/tests/create", "AITestCasesCreate")]
    public async Task<IActionResult> CreatePost(string sourceSessionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var testCase = new AITestCase
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = _clock.UtcNow,
            SourceSessionId = string.IsNullOrWhiteSpace(sourceSessionId) ? null : sourceSessionId,
        };

        var editor = await _displayManager.UpdateEditorAsync(testCase, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = S["New test case"], Editor = editor });
        }

        await _store.CreateAsync(testCase);
        await _notifier.SuccessAsync(H["The test case was created."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Makes a test case from a real conversation and opens it to review.
    /// </summary>
    /// <param name="sessionId">The conversation.</param>
    /// <param name="sessionManager">The chat session manager.</param>
    /// <param name="promptStore">The chat session prompt store.</param>
    /// <returns>The form, filled in from the conversation.</returns>
    [Admin("ai/tests/from-conversation/{sessionId}", "AITestCasesFromConversation")]
    public async Task<IActionResult> FromConversation(
        string sessionId,
        [FromServices] IAIChatSessionManager sessionManager,
        [FromServices] IAIChatSessionPromptStore promptStore)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var session = string.IsNullOrEmpty(sessionId) ? null : await sessionManager.FindByIdAsync(sessionId);

        if (session is null)
        {
            return NotFound();
        }

        var testCase = AITestCaseFactory.FromConversation(session, await promptStore.GetPromptsAsync(session.SessionId));
        ViewData["SourceSessionId"] = session.SessionId;

        return View(nameof(Create), new EditCatalogEntryViewModel
        {
            DisplayName = S["New test case"],
            Editor = await _displayManager.BuildEditorAsync(testCase, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Shows a test case's form.
    /// </summary>
    /// <param name="id">The test case.</param>
    /// <returns>The form.</returns>
    [Admin("ai/tests/edit/{id}", "AITestCasesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var testCase = await _store.FindByIdAsync(id);

        if (testCase is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = testCase.Name,
            Editor = await _displayManager.BuildEditorAsync(testCase, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a test case.
    /// </summary>
    /// <param name="id">The test case.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/tests/edit/{id}", "AITestCasesEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var testCase = await _store.FindByIdAsync(id);

        if (testCase is null)
        {
            return NotFound();
        }

        var editor = await _displayManager.UpdateEditorAsync(testCase, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = testCase.Name, Editor = editor });
        }

        testCase.ModifiedUtc = _clock.UtcNow;
        await _store.UpdateAsync(testCase);
        await _notifier.SuccessAsync(H["The test case was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a test case. Its runs are kept.
    /// </summary>
    /// <param name="id">The test case.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("ai/tests/delete/{id}", "AITestCasesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var testCase = await _store.FindByIdAsync(id);

        if (testCase is not null)
        {
            await _store.DeleteAsync(testCase);
            await _notifier.SuccessAsync(H["The test case was deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Queues runs of one test case.
    /// </summary>
    /// <param name="id">The test case.</param>
    /// <param name="repeats">How many times to run it.</param>
    /// <returns>A redirect to the batch's runs.</returns>
    [HttpPost]
    [Admin("ai/tests/run/{id}", "AITestCasesRun")]
    public async Task<IActionResult> Run(string id, int repeats = 1)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var testCase = await _store.FindByIdAsync(id);

        if (testCase is null)
        {
            return NotFound();
        }

        return await QueueAndShowAsync([testCase], repeats);
    }

    /// <summary>
    /// Lists runs: of one test, of one batch, or every run.
    /// </summary>
    /// <param name="testCaseId">The test, if any.</param>
    /// <param name="batchId">The batch, if any.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The runs.</returns>
    [Admin("ai/tests/runs", "AITestCasesRuns")]
    public async Task<IActionResult> Runs(
        string testCaseId,
        string batchId,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        IReadOnlyList<AITestRun> runs;
        int count;

        if (!string.IsNullOrEmpty(batchId))
        {
            var batch = await _runStore.ListByBatchAsync(batchId);
            count = batch.Count;
            runs = batch.Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize).ToList();
        }
        else
        {
            (runs, count) = await _runStore.PageAsync(testCaseId, pager.Page, pager.PageSize);
        }

        var routeData = new RouteData();
        routeData.Values.TryAdd(nameof(testCaseId), testCaseId);
        routeData.Values.TryAdd(nameof(batchId), batchId);

        var pagerShape = await shapeFactory.PagerAsync(pager, count, routeData);
        var testCase = string.IsNullOrEmpty(testCaseId) ? null : await _store.FindByIdAsync(testCaseId);
        var all = !string.IsNullOrEmpty(batchId) ? await _runStore.ListByBatchAsync(batchId) : runs;

        return View(new AITestRunsViewModel
        {
            TestCase = testCase,
            BatchId = batchId,
            PassRate = AITestRunner.PassRate(all),
            Pending = all.Count(run => run.Status is AITestRunStatus.Queued or AITestRunStatus.Running),
            List = new AdminListContext<AITestRun>
            {
                Source = runs,
                Pager = pagerShape,
            },
        });
    }

    /// <summary>
    /// Shows one run: the conversation and the verdict.
    /// </summary>
    /// <param name="id">The run.</param>
    /// <returns>The run.</returns>
    [Admin("ai/tests/runs/{id}", "AITestCasesRunDetails")]
    public async Task<IActionResult> RunDetails(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var run = await _runStore.FindByIdAsync(id);

        return run is null
            ? NotFound()
            : View(run);
    }

    private async Task<IActionResult> QueueAndShowAsync(IEnumerable<AITestCase> testCases, int repeats)
    {
        var (batchId, runs) = await _runner.QueueAsync(testCases, repeats, User.Identity?.Name);

        await _notifier.SuccessAsync(H["{0} run(s) queued. They start within a minute; this page shows them as they finish.", runs.Count]);

        return RedirectToAction(nameof(Runs), new { batchId });
    }
}
