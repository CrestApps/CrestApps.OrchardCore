using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.BackgroundJobs;
using OrchardCore.DataSources;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.DataConnectors.Controllers;

/// <summary>
/// Tools › Data Connectors › Data Sets: what reports and data pipelines read from the connections, live or imported,
/// with each imported data set's refreshes.
/// </summary>
[Admin("data-connectors/data-sets/{action}/{id?}", "ConnectedDataSets{action}")]
public sealed class ConnectedDataSetsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<ConnectedDataSet> _displayManager;
    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly IDataConnectionStore _connectionStore;
    private readonly IDataSetRefreshRunStore _runStore;
    private readonly IDataSetRowStore _rowStore;
    private readonly IDataConnectorManager _connectorManager;
    private readonly ConnectedDataReader _reader;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly DataConnectorsOptions _options;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectedDataSetsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The data sets' display manager.</param>
    /// <param name="dataSetStore">The data sets.</param>
    /// <param name="connectionStore">The connections.</param>
    /// <param name="runStore">The refreshes.</param>
    /// <param name="rowStore">The rows of imported data sets.</param>
    /// <param name="connectorManager">The registered connectors.</param>
    /// <param name="reader">The reader of external systems.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The limits of reads.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ConnectedDataSetsController(
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<ConnectedDataSet> displayManager,
        IConnectedDataSetStore dataSetStore,
        IDataConnectionStore connectionStore,
        IDataSetRefreshRunStore runStore,
        IDataSetRowStore rowStore,
        IDataConnectorManager connectorManager,
        ConnectedDataReader reader,
        INotifier notifier,
        IClock clock,
        IOptions<DataConnectorsOptions> options,
        ILogger<ConnectedDataSetsController> logger,
        IHtmlLocalizer<ConnectedDataSetsController> htmlLocalizer,
        IStringLocalizer<ConnectedDataSetsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _dataSetStore = dataSetStore;
        _connectionStore = connectionStore;
        _runStore = runStore;
        _rowStore = rowStore;
        _connectorManager = connectorManager;
        _reader = reader;
        _notifier = notifier;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the data sets.
    /// </summary>
    [Admin("data-connectors/data-sets", "ConnectedDataSetsIndex")]
    public async Task<IActionResult> Index(
        DataConnectorListOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageConnectedDataSets))
        {
            return Forbid();
        }

        options ??= new DataConnectorListOptions();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        IEnumerable<ConnectedDataSet> dataSets = await _dataSetStore.ListAsync(HttpContext.RequestAborted);

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            dataSets = dataSets.Where(dataSet =>
                dataSet.DisplayText?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) == true ||
                dataSet.Name?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) == true);
        }

        var all = dataSets.ToArray();
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        // Only the connections whose connector is enabled can feed a new data set.
        var connections = (await _connectionStore.ListAsync(HttpContext.RequestAborted))
            .Where(connection => _connectorManager.GetDescriptor(connection.Connector) is not null)
            .ToArray();

        return View(new ConnectedDataSetsIndexViewModel
        {
            Options = options,
            Connections = connections,
            List = new AdminListContext<ConnectedDataSet>
            {
                Source = [.. all.Skip(pager.GetStartIndex()).Take(pager.PageSize)],
                Pager = (IShape)await shapeFactory.PagerAsync(pager, all.Length, routeData),
            },
        });
    }

    /// <summary>
    /// Searches the data sets.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("data-connectors/data-sets", "ConnectedDataSetsIndex")]
    public async Task<IActionResult> IndexFilterPost(ConnectedDataSetsIndexViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageConnectedDataSets))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Shows the editor of a new data set of a connection.
    /// </summary>
    /// <param name="id">The connection's identifier.</param>
    public async Task<IActionResult> Create(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageConnectedDataSets))
        {
            return Forbid();
        }

        var dataSet = await NewDataSetAsync(id);

        if (dataSet is null)
        {
            await _notifier.ErrorAsync(H["The connection doesn't exist, or its connector isn't enabled."]);

            return RedirectToAction(nameof(Index));
        }

        return View("Edit", await BuildEditAsync(dataSet, isNew: true));
    }

    /// <summary>
    /// Creates a data set, or discovers its fields when <c>submit.DiscoverFields</c> was clicked.
    /// </summary>
    /// <param name="id">The connection's identifier.</param>
    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageConnectedDataSets))
        {
            return Forbid();
        }

        var dataSet = await NewDataSetAsync(id);

        if (dataSet is null)
        {
            return NotFound();
        }

        return await SaveAsync(dataSet, isNew: true);
    }

    /// <summary>
    /// Shows the editor of a data set.
    /// </summary>
    /// <param name="id">The data set's identifier.</param>
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageConnectedDataSets))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } dataSet)
        {
            return NotFound();
        }

        return View(await BuildEditAsync(dataSet, isNew: false));
    }

    /// <summary>
    /// Saves a data set, or discovers its fields when <c>submit.DiscoverFields</c> was clicked.
    /// </summary>
    /// <param name="id">The data set's identifier.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageConnectedDataSets))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } dataSet)
        {
            return NotFound();
        }

        return await SaveAsync(dataSet, isNew: false);
    }

    /// <summary>
    /// Shows the first rows of a data set, read the way reports and data pipelines read it.
    /// </summary>
    /// <param name="id">The data set's identifier.</param>
    /// <param name="dataSourceManager">The data sources.</param>
    public async Task<IActionResult> Preview(string id, [FromServices] IDataSourceManager dataSourceManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageConnectedDataSets))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } dataSet)
        {
            return NotFound();
        }

        var sourceName = dataSet.AccessMode == DataSetAccessMode.Live ? DataConnectorConstants.LiveDataSourceName : DataConnectorConstants.ImportedDataSourceName;
        var model = new DataSetPreviewViewModel
        {
            DataSet = dataSet,
            SourceName = sourceName,
        };

        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.CreateReadPermission(dataSet.Name)))
        {
            model.Error = S["You can manage this data set but not read it. Grant your role the permission '{0}' to see its rows.", DataConnectorPermissions.CreateReadPermission(dataSet.Name).Name];

            return View(model);
        }

        if (!dataSet.IsEnabled)
        {
            model.Error = S["The data set is disabled, so reports and data pipelines can't read it."];

            return View(model);
        }

        try
        {
            var dataSource = dataSourceManager.GetDataSource(sourceName);
            var query = new DataSourceQuery
            {
                DataSet = dataSet.Name,
                Context = new DataSourceContext { User = User },
            };

            var rows = await dataSource.ReadRowsAsync(query, Math.Max(1, _options.PreviewRows), HttpContext.RequestAborted);

            model.Fields = rows.Fields;
            model.Rows = rows.Rows;
            model.Truncated = rows.Truncated;
        }
        catch (DataConnectorException exception)
        {
            model.Error = exception.Message;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The preview of the data set '{DataSet}' failed.", dataSet.Name);
            model.Error = S["The rows couldn't be read. See the site's log for details."];
        }

        return View(model);
    }

    /// <summary>
    /// Shows the refreshes of an imported data set.
    /// </summary>
    /// <param name="id">The data set's identifier.</param>
    /// <param name="pagerParameters">The page.</param>
    /// <param name="pagerOptions">The pager's options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    public async Task<IActionResult> History(
        string id,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageConnectedDataSets))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } dataSet)
        {
            return NotFound();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, runs) = await _runStore.PageAsync(dataSet.ItemId, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();
        routeData.Values.TryAdd("id", dataSet.ItemId);

        return View(new DataSetRunsViewModel
        {
            DataSet = dataSet,
            List = new AdminListContext<DataSetRefreshRun>
            {
                Source = runs,
                Pager = (IShape)await shapeFactory.PagerAsync(pager, count, routeData),
            },
        });
    }

    /// <summary>
    /// Refreshes an imported data set now, after this request.
    /// </summary>
    /// <param name="id">The data set's identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Refresh(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.RefreshConnectedDataSets))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } dataSet)
        {
            return NotFound();
        }

        if (dataSet.AccessMode != DataSetAccessMode.Imported)
        {
            await _notifier.WarningAsync(H["Only imported data sets are refreshed; a live data set is read from its source every time."]);

            return RedirectToAction(nameof(Index));
        }

        await RequestRefreshAsync(dataSet);
        await _notifier.SuccessAsync(H["The refresh of '{0}' started. Its result shows in the list and in its history.", dataSet.DisplayText]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a data set, its imported rows and its refreshes.
    /// </summary>
    /// <param name="id">The data set's identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageConnectedDataSets))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } dataSet)
        {
            return NotFound();
        }

        await _rowStore.DeleteAllAsync(dataSet.ItemId, cancellationToken: HttpContext.RequestAborted);
        await _runStore.PruneAsync(dataSet.ItemId, 0, HttpContext.RequestAborted);
        await _dataSetStore.DeleteAsync(dataSet, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The data set was deleted. Reports and data pipelines that read it will find it missing."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(ConnectedDataSet dataSet, bool isNew)
    {
        var editor = await _displayManager.UpdateEditorAsync(dataSet, _updateModelAccessor.ModelUpdater, isNew);

        if (Request.Form.ContainsKey("submit.DiscoverFields"))
        {
            // The posted values are kept on the record, unsaved, so the fields are read with what was just typed.
            if (await TryDiscoverFieldsAsync(dataSet))
            {
                ModelState.Clear();
                await _notifier.SuccessAsync(H["Found {0} field(s). Review them, then save.", dataSet.Fields.Count]);
            }

            return View("Edit", await BuildEditAsync(dataSet, isNew));
        }

        // A data set saved before its fields were read gets them now, so it can be read at once.
        if (ModelState.IsValid && dataSet.Fields.Count == 0)
        {
            await TryDiscoverFieldsAsync(dataSet);
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", new DataConnectorEditViewModel
            {
                Title = isNew ? H["New data set"] : H["Edit data set: {0}", dataSet.DisplayText],
                ItemId = isNew ? null : dataSet.ItemId,
                Editor = editor,
                IsDataSet = true,
                IsImported = dataSet.AccessMode == DataSetAccessMode.Imported,
            });
        }

        var importNow = dataSet.AccessMode == DataSetAccessMode.Imported && (isNew || string.IsNullOrEmpty(dataSet.Import?.ActiveVersion));

        if (isNew)
        {
            await _dataSetStore.CreateAsync(dataSet, HttpContext.RequestAborted);
        }
        else
        {
            dataSet.ModifiedUtc = _clock.UtcNow;
            await _dataSetStore.UpdateAsync(dataSet, HttpContext.RequestAborted);
        }

        if (importNow && await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.RefreshConnectedDataSets))
        {
            await RequestRefreshAsync(dataSet);
            await _notifier.SuccessAsync(H["The data set was saved, and its first import started."]);
        }
        else
        {
            await _notifier.SuccessAsync(H["The data set was saved."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> TryDiscoverFieldsAsync(ConnectedDataSet dataSet)
    {
        try
        {
            var sourceFields = await _reader.DescribeAsync(dataSet, HttpContext.RequestAborted);

            if (sourceFields.Count == 0)
            {
                ModelState.AddModelError(string.Empty, S["The source described no fields. Check the resource and its settings."]);

                return false;
            }

            if (dataSet.Fields.Count == 0)
            {
                dataSet.Fields = ConnectedDataSetFields.Create(sourceFields);
            }
            else
            {
                ConnectedDataSetFields.Merge(dataSet.Fields, sourceFields);
            }

            return true;
        }
        catch (DataConnectorException exception)
        {
            ModelState.AddModelError(string.Empty, S["The fields couldn't be read: {0}", exception.Message]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The fields of the data set '{DataSet}' couldn't be read.", dataSet.Name);
            ModelState.AddModelError(string.Empty, S["The fields couldn't be read. See the site's log for details."]);
        }

        return false;
    }

    private async Task RequestRefreshAsync(ConnectedDataSet dataSet)
    {
        dataSet.Import ??= new DataSetImportState();
        dataSet.Import.RefreshRequestedUtc = _clock.UtcNow;
        await _dataSetStore.UpdateAsync(dataSet, HttpContext.RequestAborted);

        var state = (dataSet.ItemId, User.Identity?.Name);

        // The refresh runs after this request, in its own scope; if the site stops first, the request stays on the
        // data set and the refresh task runs it.
        ShellScope.AddDeferredTask(scope => HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
            $"data-set-refresh-{dataSet.ItemId}",
            state,
            static async (backgroundScope, request) =>
            {
                var store = backgroundScope.ServiceProvider.GetRequiredService<IConnectedDataSetStore>();

                if (await store.FindByIdAsync(request.ItemId) is not { Import.RefreshRequestedUtc: not null } current)
                {
                    return;
                }

                var refreshService = backgroundScope.ServiceProvider.GetRequiredService<IDataSetRefreshService>();

                await refreshService.RefreshAsync(current, DataSetRefreshTrigger.Manual, request.Name);
            }));
    }

    private async Task<DataConnectorEditViewModel> BuildEditAsync(ConnectedDataSet dataSet, bool isNew)
        => new()
        {
            Title = isNew ? H["New data set"] : H["Edit data set: {0}", dataSet.DisplayText],
            ItemId = isNew ? null : dataSet.ItemId,
            Editor = await _displayManager.BuildEditorAsync(dataSet, _updateModelAccessor.ModelUpdater, isNew),
            IsDataSet = true,
            IsImported = dataSet.AccessMode == DataSetAccessMode.Imported,
        };

    private async Task<ConnectedDataSet> NewDataSetAsync(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId) ||
            await _connectionStore.FindByIdAsync(connectionId, HttpContext.RequestAborted) is not { } connection ||
            _connectorManager.GetDescriptor(connection.Connector) is not { } descriptor)
        {
            return null;
        }

        return new ConnectedDataSet
        {
            ItemId = IdGenerator.GenerateId(),
            ConnectionId = connection.ItemId,
            AccessMode = descriptor.Supports(DataConnectorCapabilities.Live) ? DataSetAccessMode.Live : DataSetAccessMode.Imported,
            CreatedUtc = _clock.UtcNow,
            Author = User.Identity?.Name,
        };
    }

    private async Task<ConnectedDataSet> FindAsync(string id)
        => string.IsNullOrEmpty(id) ? null : await _dataSetStore.FindByIdAsync(id, HttpContext.RequestAborted);
}
