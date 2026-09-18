using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Services;
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
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Controllers;

/// <summary>
/// Manages file sources in the admin area. Each registered ingestion connector (file system, FTP, SFTP) is
/// a source, so the create flow mirrors the other source-based catalog editors.
/// </summary>
/// <remarks>
/// A file source is stored as a <c>WebCrawler</c> record whose source names a connector rather than a crawl
/// strategy, so these screens list by connector instead of paging the whole catalog. Paging it would show
/// web crawlers here and file sources there.
/// </remarks>
public sealed class FileSourcesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISourceCatalogManager<WebCrawler> _manager;
    private readonly IDisplayManager<WebCrawler> _displayManager;
    private readonly IFileSourceRunService _runService;
    private readonly IReadOnlyList<IngestionConnectorDescriptor> _connectors;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSourcesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="manager">The file source catalog manager.</param>
    /// <param name="displayManager">The file source display manager.</param>
    /// <param name="runService">The run service.</param>
    /// <param name="connectorOptions">The registered ingestion connectors.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FileSourcesController(
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        ISourceCatalogManager<WebCrawler> manager,
        IDisplayManager<WebCrawler> displayManager,
        IFileSourceRunService runService,
        IOptions<IngestionConnectorOptions> connectorOptions,
        INotifier notifier,
        IHtmlLocalizer<FileSourcesController> htmlLocalizer,
        IStringLocalizer<FileSourcesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _manager = manager;
        _displayManager = displayManager;
        _runService = runService;
        _connectors = connectorOptions.Value.Connectors
            .OrderBy(connector => connector.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays a paginated list of file sources.
    /// </summary>
    [Admin("ai/file-sources", "FileSourcesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FileSourcePermissions.ManageFileSources))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());
        var records = await LoadFileSourcesAsync(options.Search);

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListSourceModelViewModel<IngestionConnectorDescriptor, CatalogEntryViewModel<WebCrawler>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, records.Count, routeData),
            Sources = _connectors,
        };

        foreach (var model in records.Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize))
        {
            viewModel.Models.Add(new CatalogEntryViewModel<WebCrawler>
            {
                Model = model,
                Shape = await _displayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    /// <summary>
    /// Handles the filter form submission for the file sources index page.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/file-sources", "FileSourcesIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FileSourcePermissions.ManageFileSources))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the form for creating a new file source for the given connector.
    /// </summary>
    /// <param name="source">The connector name.</param>
    [Admin("ai/file-source/create/{source}", "FileSourcesCreate")]
    public async Task<ActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FileSourcePermissions.ManageFileSources))
        {
            return Forbid();
        }

        if (!TryGetConnector(source, out var connector))
        {
            await _notifier.ErrorAsync(H["Unable to find a connector with the name '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var fileSource = await _manager.NewAsync(connector.Name);

        if (fileSource == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new file source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = connector.DisplayName.Value,
            Editor = await _displayManager.BuildEditorAsync(fileSource, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for creating a new file source.
    /// </summary>
    /// <param name="source">The connector name.</param>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/file-source/create/{source}", "FileSourcesCreate")]
    public async Task<ActionResult> CreatePost(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FileSourcePermissions.ManageFileSources))
        {
            return Forbid();
        }

        if (!TryGetConnector(source, out var connector))
        {
            await _notifier.ErrorAsync(H["Unable to find a connector with the name '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var fileSource = await _manager.NewAsync(connector.Name);

        if (fileSource == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new file source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = connector.DisplayName.Value,
            Editor = await _displayManager.UpdateEditorAsync(fileSource, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(fileSource);

            await _notifier.SuccessAsync(H["File source has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Displays the form for editing an existing file source.
    /// </summary>
    /// <param name="id">The unique identifier of the file source to edit.</param>
    [Admin("ai/file-source/edit/{id}", "FileSourcesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FileSourcePermissions.ManageFileSources))
        {
            return Forbid();
        }

        var fileSource = await FindFileSourceAsync(id);

        if (fileSource == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = fileSource.DisplayText,
            Editor = await _displayManager.BuildEditorAsync(fileSource, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for editing an existing file source.
    /// </summary>
    /// <param name="id">The unique identifier of the file source to update.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/file-source/edit/{id}", "FileSourcesEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FileSourcePermissions.ManageFileSources))
        {
            return Forbid();
        }

        var fileSource = await FindFileSourceAsync(id);

        if (fileSource == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = fileSource.DisplayText,
            Editor = await _displayManager.UpdateEditorAsync(fileSource, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(fileSource);

            await _notifier.SuccessAsync(H["File source has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Deletes a file source by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the file source to delete.</param>
    [HttpPost]
    [Admin("ai/file-source/delete/{id}", "FileSourcesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FileSourcePermissions.ManageFileSources))
        {
            return Forbid();
        }

        var fileSource = await FindFileSourceAsync(id);

        if (fileSource == null)
        {
            return NotFound();
        }

        if (await _manager.DeleteAsync(fileSource))
        {
            await _notifier.SuccessAsync(H["File source has been deleted successfully. Knowledge-base cleanup has been queued."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the file source."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Reads this file source now rather than waiting for its schedule.
    /// </summary>
    /// <param name="id">The unique identifier of the file source to run.</param>
    [HttpPost]
    [Admin("ai/file-source/run/{id}", "FileSourcesRun")]
    public async Task<IActionResult> Run(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FileSourcePermissions.ManageFileSources))
        {
            return Forbid();
        }

        var fileSource = await FindFileSourceAsync(id);

        if (fileSource == null)
        {
            return NotFound();
        }

        var summary = await _runService.RunAsync(fileSource, HttpContext.RequestAborted);

        if (summary.Status == FileSourceRunStatus.Failed)
        {
            await _notifier.ErrorAsync(H["The file source could not be read. {0}", summary.Error]);
        }
        else if (!summary.DiscoveryCompleted)
        {
            // Worth saying plainly: a run that saw only part of the folder deletes nothing, because
            // deciding something is gone needs a listing of the whole folder in one pass.
            await _notifier.WarningAsync(H["Read {0} of {1} item(s); {2} failed. This run saw only part of the source, so nothing was removed and the next run resumes where this one stopped.",
                summary.ItemsIndexed, summary.ItemsDiscovered, summary.ItemsFailed]);
        }
        else
        {
            await _notifier.SuccessAsync(H["Read {0} of {1} item(s); {2} removed, {3} failed.",
                summary.ItemsIndexed, summary.ItemsDiscovered, summary.ItemsDeleted, summary.ItemsFailed]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Handles bulk actions on selected file sources.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/file-sources", "FileSourcesIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, FileSourcePermissions.ManageFileSources))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case CatalogEntryAction.None:
                    break;
                case CatalogEntryAction.Remove:
                    var counter = 0;

                    foreach (var id in itemIds)
                    {
                        var fileSource = await FindFileSourceAsync(id);

                        if (fileSource == null)
                        {
                            continue;
                        }

                        if (await _manager.DeleteAsync(fileSource))
                        {
                            counter++;
                        }
                    }

                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No file sources were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 file source has been removed successfully.", "{0} file sources have been removed successfully."));
                    }

                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Loads every record whose source is one of this tenant's registered connectors.
    /// </summary>
    /// <param name="search">An optional name filter.</param>
    /// <returns>The matching file sources, ordered by name.</returns>
    private async Task<IReadOnlyList<WebCrawler>> LoadFileSourcesAsync(string search)
    {
        var records = new List<WebCrawler>();

        foreach (var connector in _connectors)
        {
            records.AddRange(await _manager.GetAsync(connector.Name));
        }

        IEnumerable<WebCrawler> matches = records;

        if (!string.IsNullOrWhiteSpace(search))
        {
            matches = matches.Where(record =>
                record.DisplayText?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        }

        return matches
            .OrderBy(record => record.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Finds a record and refuses one that is not a file source, so a web crawler's id cannot be edited,
    /// run or deleted through these screens.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <returns>The file source, or <see langword="null"/>.</returns>
    private async Task<WebCrawler> FindFileSourceAsync(string id)
    {
        var record = await _manager.FindByIdAsync(id);

        return record is not null && FileSourceRecords.IsConnector(record.Source, _connectors)
            ? record
            : null;
    }

    private bool TryGetConnector(string source, out IngestionConnectorDescriptor connector)
    {
        connector = null;

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        connector = _connectors.FirstOrDefault(entry =>
            string.Equals(entry.Name, source, StringComparison.OrdinalIgnoreCase));

        return connector != null;
    }
}
