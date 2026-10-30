using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.DataConnectors.Controllers;

/// <summary>
/// Tools › Data Connectors › Connections: how the site reaches external systems, with their credentials.
/// </summary>
[Admin("data-connectors/connections/{action}/{id?}", "DataConnections{action}")]
public sealed class DataConnectionsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<DataConnection> _displayManager;
    private readonly IDataConnectionStore _connectionStore;
    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly IDataConnectorManager _connectorManager;
    private readonly ConnectedDataReader _reader;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectionsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The connections' display manager.</param>
    /// <param name="connectionStore">The connections.</param>
    /// <param name="dataSetStore">The data sets.</param>
    /// <param name="connectorManager">The registered connectors.</param>
    /// <param name="reader">The reader of external systems.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataConnectionsController(
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<DataConnection> displayManager,
        IDataConnectionStore connectionStore,
        IConnectedDataSetStore dataSetStore,
        IDataConnectorManager connectorManager,
        ConnectedDataReader reader,
        INotifier notifier,
        IClock clock,
        ILogger<DataConnectionsController> logger,
        IHtmlLocalizer<DataConnectionsController> htmlLocalizer,
        IStringLocalizer<DataConnectionsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _connectionStore = connectionStore;
        _dataSetStore = dataSetStore;
        _connectorManager = connectorManager;
        _reader = reader;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the connections.
    /// </summary>
    [Admin("data-connectors/connections", "DataConnectionsIndex")]
    public async Task<IActionResult> Index(
        DataConnectorListOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageDataConnections))
        {
            return Forbid();
        }

        options ??= new DataConnectorListOptions();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        IEnumerable<DataConnection> connections = await _connectionStore.ListAsync(HttpContext.RequestAborted);

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            connections = connections.Where(connection =>
                connection.DisplayText?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) == true ||
                connection.Name?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) == true);
        }

        var all = connections.ToArray();
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        return View(new DataConnectionsIndexViewModel
        {
            Options = options,
            Connectors = [.. _connectorManager.GetConnectors().OrderBy(connector => connector.DisplayName.Value, StringComparer.OrdinalIgnoreCase)],
            List = new AdminListContext<DataConnection>
            {
                Source = [.. all.Skip(pager.GetStartIndex()).Take(pager.PageSize)],
                Pager = (IShape)await shapeFactory.PagerAsync(pager, all.Length, routeData),
            },
        });
    }

    /// <summary>
    /// Searches the connections.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("data-connectors/connections", "DataConnectionsIndex")]
    public async Task<IActionResult> IndexFilterPost(DataConnectionsIndexViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageDataConnections))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Shows the editor of a new connection of a connector.
    /// </summary>
    /// <param name="id">The connector's name.</param>
    public async Task<IActionResult> Create(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageDataConnections))
        {
            return Forbid();
        }

        if (_connectorManager.GetDescriptor(id) is not { } descriptor)
        {
            await _notifier.ErrorAsync(H["No connector is named '{0}'.", id ?? string.Empty]);

            return RedirectToAction(nameof(Index));
        }

        var connection = NewConnection(descriptor);

        return View("Edit", new DataConnectorEditViewModel
        {
            Title = H["New {0} connection", descriptor.DisplayName.Value],
            Editor = await _displayManager.BuildEditorAsync(connection, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Creates a connection, and checks it when <c>submit.Test</c> was clicked.
    /// </summary>
    /// <param name="id">The connector's name.</param>
    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageDataConnections))
        {
            return Forbid();
        }

        if (_connectorManager.GetDescriptor(id) is not { } descriptor)
        {
            return NotFound();
        }

        var connection = NewConnection(descriptor);
        var editor = await _displayManager.UpdateEditorAsync(connection, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View("Edit", new DataConnectorEditViewModel
            {
                Title = H["New {0} connection", descriptor.DisplayName.Value],
                Editor = editor,
            });
        }

        await _connectionStore.CreateAsync(connection, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The connection was added."]);

        if (Request.Form.ContainsKey("submit.Test"))
        {
            await TestAndNotifyAsync(connection);

            return RedirectToAction(nameof(Edit), new { id = connection.ItemId });
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the editor of a connection.
    /// </summary>
    /// <param name="id">The connection's identifier.</param>
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageDataConnections))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id) || await _connectionStore.FindByIdAsync(id, HttpContext.RequestAborted) is not { } connection)
        {
            return NotFound();
        }

        return View(new DataConnectorEditViewModel
        {
            Title = H["Edit connection: {0}", connection.DisplayText],
            ItemId = connection.ItemId,
            Editor = await _displayManager.BuildEditorAsync(connection, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a connection, and checks it when <c>submit.Test</c> was clicked.
    /// </summary>
    /// <param name="id">The connection's identifier.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageDataConnections))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id) || await _connectionStore.FindByIdAsync(id, HttpContext.RequestAborted) is not { } connection)
        {
            return NotFound();
        }

        var editor = await _displayManager.UpdateEditorAsync(connection, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!ModelState.IsValid)
        {
            return View(new DataConnectorEditViewModel
            {
                Title = H["Edit connection: {0}", connection.DisplayText],
                ItemId = connection.ItemId,
                Editor = editor,
            });
        }

        connection.ModifiedUtc = _clock.UtcNow;
        await _connectionStore.UpdateAsync(connection, HttpContext.RequestAborted);

        if (Request.Form.ContainsKey("submit.Test"))
        {
            await _notifier.SuccessAsync(H["The connection was saved."]);
            await TestAndNotifyAsync(connection);

            return RedirectToAction(nameof(Edit), new { id });
        }

        await _notifier.SuccessAsync(H["The connection was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Checks that a saved connection reaches its system.
    /// </summary>
    /// <param name="id">The connection's identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Test(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageDataConnections))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id) || await _connectionStore.FindByIdAsync(id, HttpContext.RequestAborted) is not { } connection)
        {
            return NotFound();
        }

        await TestAndNotifyAsync(connection);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a connection that no data set reads.
    /// </summary>
    /// <param name="id">The connection's identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DataConnectorPermissions.ManageDataConnections))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id) || await _connectionStore.FindByIdAsync(id, HttpContext.RequestAborted) is not { } connection)
        {
            return NotFound();
        }

        var dataSets = await _dataSetStore.ListByConnectionAsync(connection.ItemId, HttpContext.RequestAborted);

        if (dataSets.Count > 0)
        {
            await _notifier.ErrorAsync(H["The connection can't be deleted while {0} data set(s) read it: {1}.", dataSets.Count, string.Join(", ", dataSets.Select(dataSet => dataSet.DisplayText))]);

            return RedirectToAction(nameof(Index));
        }

        await _connectionStore.DeleteAsync(connection, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The connection was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private DataConnection NewConnection(DataConnectorDescriptor descriptor)
        => new()
        {
            ItemId = IdGenerator.GenerateId(),
            Connector = descriptor.Name,
            CreatedUtc = _clock.UtcNow,
            Author = User.Identity?.Name,
        };

    private async Task TestAndNotifyAsync(DataConnection connection)
    {
        DataConnectorTestResult result;

        try
        {
            result = await _reader.TestAsync(connection, HttpContext.RequestAborted);
        }
        catch (DataConnectorException exception)
        {
            result = DataConnectorTestResult.Failure(exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The data connection '{Connection}' couldn't be checked.", connection.Name);
            result = DataConnectorTestResult.Failure(S["The check failed unexpectedly. See the site's log for details."]);
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The connection works. {0}", result.Message ?? string.Empty]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The connection doesn't work: {0}", result.Message ?? string.Empty]);
        }
    }
}
