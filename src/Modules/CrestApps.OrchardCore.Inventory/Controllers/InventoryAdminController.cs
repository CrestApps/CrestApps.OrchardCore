using System.Security.Claims;
using System.Text;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Modules;
using OrchardCore.Routing;
using YesSql;

namespace CrestApps.OrchardCore.Inventory.Controllers;

/// <summary>
/// Commerce > Inventory: what is on hand, what is low, and the history of every change.
/// </summary>
public sealed class InventoryAdminController : Controller
{
    private const string OptionsSearch = "Options.Search";
    private const string OptionsFilter = "Options.Filter";
    private const int ExportPageSize = 500;
    private const int MaxImportRows = 10_000;
    private const long MaxImportBytes = 2 * 1024 * 1024;
    private const int MaxProblemsShown = 10;

    private readonly IStockItemStore _items;
    private readonly IStockMovementStore _movements;
    private readonly IInventoryService _inventory;
    private readonly IInventoryLocationService _locations;
    private readonly InventorySyncService _sync;
    private readonly StockCountImporter _importer;
    private readonly IClock _clock;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;
    private readonly ISession _session;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public InventoryAdminController(
        IStockItemStore items,
        IStockMovementStore movements,
        IInventoryService inventory,
        IInventoryLocationService locations,
        InventorySyncService sync,
        StockCountImporter importer,
        IClock clock,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        INotifier notifier,
        ISession session,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<InventoryAdminController> stringLocalizer,
        IHtmlLocalizer<InventoryAdminController> htmlLocalizer)
    {
        _items = items;
        _movements = movements;
        _inventory = inventory;
        _locations = locations;
        _sync = sync;
        _importer = importer;
        _clock = clock;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        _session = session;
        _pagerOptions = pagerOptions.Value;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [Admin("inventory", "InventoryIndex")]
    public async Task<IActionResult> Index(InventoryIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ViewInventory))
        {
            return Forbid();
        }

        options ??= new InventoryIndexOptions();

        var pager = new Pager(pagerParameters, _pagerOptions);
        var (count, items) = await _items.PageAsync(new StockItemQuery { Search = options.Search, Filter = options.Filter }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        options.Filters =
        [
            new SelectListItem(S["All"], nameof(StockItemFilter.All), options.Filter == StockItemFilter.All),
            new SelectListItem(S["Low stock"], nameof(StockItemFilter.Low), options.Filter == StockItemFilter.Low),
            new SelectListItem(S["Out of stock"], nameof(StockItemFilter.Out), options.Filter == StockItemFilter.Out),
            new SelectListItem(S["Backordered"], nameof(StockItemFilter.Backordered), options.Filter == StockItemFilter.Backordered),
        ];

        var routeData = new RouteData();

        foreach (var (key, value) in FilterRouteValues(options, null))
        {
            routeData.Values.TryAdd(key, value);
        }

        var pagerShape = await _shapeFactory.PagerAsync(pager, count, routeData);

        return View(new InventoryIndexViewModel
        {
            Options = options,
            Items = items,
            TotalCount = count,
            CanManage = await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory),
            Pager = pagerShape,

            // The admin-list tag builds a row per stock item, in the layout (List or Grid) the site or the user picked.
            List = new AdminListContext<StockItem>
            {
                Source = items,
                Pager = pagerShape,

                // The "Show" filter renders at the end of the list's toolbar.
                ToolbarActions = await _shapeFactory.CreateAsync("InventoryAdminListFilters", Arguments.From(new { Options = options })),
            },
        });
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("inventory", "InventoryIndex")]
    public async Task<IActionResult> IndexFilterPost(InventoryIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ViewInventory))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), FilterRouteValues(options, pagerParameters));
    }

    /// <summary>
    /// Starts counting products whose type gained the Inventory part, and refreshes titles, SKUs and thresholds.
    /// </summary>
    [HttpPost]
    [Admin("inventory/sync", "InventorySync")]
    public async Task<IActionResult> Sync()
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        var count = await _sync.SyncAllAsync(HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["{0} products were checked for stock to count.", count]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Downloads every product and variant counted, with its units on hand, as CSV.
    /// </summary>
    [Admin("inventory/export", "InventoryExport")]
    public async Task<IActionResult> Export()
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ViewInventory))
        {
            return Forbid();
        }

        var csv = new StringWriter();

        StockCsv.WriteHeader(csv);

        for (var skip = 0; ; skip += ExportPageSize)
        {
            var page = await _items.ListForExportAsync(skip, ExportPageSize, HttpContext.RequestAborted);

            foreach (var item in page)
            {
                StockCsv.Write(csv, item);
            }

            if (page.Count < ExportPageSize)
            {
                break;
            }
        }

        // With a byte order mark, so spreadsheet programs read accented titles right.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        return File(bytes, "text/csv", $"stock-{_clock.UtcNow:yyyy-MM-dd}.csv");
    }

    /// <summary>
    /// Sets the units on hand from a CSV file, as counts: each item whose count differs gets a Counted movement.
    /// Nothing is changed unless the whole file can be applied.
    /// </summary>
    [HttpPost]
    [Admin("inventory/import", "InventoryImport")]
    public async Task<IActionResult> Import(Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        if (file is null || file.Length == 0)
        {
            await _notifier.ErrorAsync(H["Choose a CSV file to import."]);

            return RedirectToAction(nameof(Index));
        }

        if (file.Length > MaxImportBytes)
        {
            await _notifier.ErrorAsync(H["The file is too large. Import at most {0} rows at a time.", MaxImportRows]);

            return RedirectToAction(nameof(Index));
        }

        IReadOnlyList<StockCountRow> rows;

        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            rows = StockCsv.Read(reader, MaxImportRows);
        }

        if (rows is null)
        {
            await _notifier.ErrorAsync(H["The file needs an {0} column, and a {1} or {2} column to find each item. Export the stock to get a file to start from.", StockCsv.OnHand, StockCsv.Sku, StockCsv.ContentItemId]);

            return RedirectToAction(nameof(Index));
        }

        var result = await _importer.ImportAsync(rows, S["Imported from {0}.", Path.GetFileName(file.FileName)], CurrentActor(), HttpContext.RequestAborted);

        if (result.Problems.Count > 0)
        {
            // All or nothing: a file with mistakes changes no count, so it can be fixed and imported again.
            await _session.CancelAsync();

            foreach (var problem in result.Problems.Take(MaxProblemsShown))
            {
                await _notifier.ErrorAsync(problem.Kind switch
                {
                    StockCountProblemKind.InvalidCount => H["Line {0}: {1} must be a whole number of zero or more.", problem.Line, StockCsv.OnHand],
                    StockCountProblemKind.Duplicate => H["Line {0}: {1} is counted on an earlier line too.", problem.Line, problem.Item],
                    StockCountProblemKind.TooManyRows => H["The file has more than {0} rows. Split it and import each part.", MaxImportRows],
                    _ => H["Line {0}: no product or variant is counted with '{1}'.", problem.Line, problem.Item],
                });
            }

            if (result.Problems.Count > MaxProblemsShown)
            {
                await _notifier.ErrorAsync(H["{0} more lines have problems.", result.Problems.Count - MaxProblemsShown]);
            }

            await _notifier.WarningAsync(H["Nothing was imported. Fix the lines above and import the file again."]);

            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (ConcurrencyException)
        {
            await _session.CancelAsync();
            await _notifier.WarningAsync(H["Someone else changed some of this stock while it was imported. Nothing was imported; try again in a moment."]);

            return RedirectToAction(nameof(Index));
        }

        await _notifier.SuccessAsync(H["Counted {0} items. {1} already had that count.", result.Counted, result.Unchanged]);

        return RedirectToAction(nameof(Index));
    }

    [Admin("inventory/{stockItemId}", "InventoryItem")]
    public async Task<IActionResult> Item(string stockItemId, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ViewInventory))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(stockItemId) ? null : await _items.FindByIdAsync(stockItemId, HttpContext.RequestAborted);

        if (item is null)
        {
            return NotFound();
        }

        return View(await BuildItemAsync(item, pagerParameters));
    }

    [HttpPost]
    [Admin("inventory/{stockItemId}/adjust", "InventoryAdjust")]
    public async Task<IActionResult> Adjust(string stockItemId, [Bind(Prefix = "Adjust")] AdjustStockViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        if (model is null || model.Kind is not (StockMovementKind.Received or StockMovementKind.Adjusted))
        {
            return BadRequest();
        }

        var result = await CommitAsync(await _inventory.AdjustAsync(stockItemId, await KnownLocationAsync(model.LocationId), model.Kind, model.Quantity, model.Reason, CurrentActor(), HttpContext.RequestAborted));

        await NotifyAsync(result, model.Kind == StockMovementKind.Received
            ? H["{0} received. {1} on hand.", model.Quantity, result.Item?.OnHand]
            : H["Adjusted by {0}. {1} on hand.", model.Quantity, result.Item?.OnHand]);

        return RedirectToAction(nameof(Item), new { stockItemId });
    }

    [HttpPost]
    [Admin("inventory/{stockItemId}/count", "InventoryCount")]
    public async Task<IActionResult> Count(string stockItemId, [Bind(Prefix = "Count")] CountStockViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        if (model?.Count is not { } count)
        {
            await _notifier.ErrorAsync(H["Enter how many you counted."]);

            return RedirectToAction(nameof(Item), new { stockItemId });
        }

        var result = await CommitAsync(await _inventory.SetCountAsync(stockItemId, await KnownLocationAsync(model.LocationId), count, model.Reason, CurrentActor(), HttpContext.RequestAborted));

        await NotifyAsync(result, H["Counted {0}. The difference was {1}.", count, result.Movement?.Quantity]);

        return RedirectToAction(nameof(Item), new { stockItemId });
    }

    [HttpPost]
    [Admin("inventory/{stockItemId}/transfer", "InventoryTransfer")]
    public async Task<IActionResult> Transfer(string stockItemId, [Bind(Prefix = "Transfer")] TransferStockViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        if (model?.Quantity is not > 0 || string.IsNullOrEmpty(model.FromLocationId) || string.IsNullOrEmpty(model.ToLocationId))
        {
            await _notifier.ErrorAsync(H["Choose where the units come from and go to, and how many."]);

            return RedirectToAction(nameof(Item), new { stockItemId });
        }

        // Units can come from a location no longer listed, but only go to one that is.
        var locations = await _locations.GetLocationsAsync();

        if (!locations.Any(location => location.LocationId == model.ToLocationId))
        {
            return BadRequest();
        }

        var from = locations.FirstOrDefault(location => location.LocationId == model.FromLocationId)?.Name ?? model.FromLocationId;
        var to = locations.First(location => location.LocationId == model.ToLocationId).Name;
        var reason = string.IsNullOrWhiteSpace(model.Reason) ? S["From {0} to {1}", from, to].Value : model.Reason;
        var result = await _inventory.TransferAsync(stockItemId, model.FromLocationId, model.ToLocationId, model.Quantity.Value, reason, CurrentActor(), HttpContext.RequestAborted);

        if (result.Error == StockChangeError.NotEnoughAtLocation)
        {
            await _notifier.ErrorAsync(model.FromLocationId == model.ToLocationId
                ? H["Choose two different locations."]
                : H["{0} doesn't have {1} available to move. Units held for orders stay where they are.", from, model.Quantity]);

            return RedirectToAction(nameof(Item), new { stockItemId });
        }

        result = await CommitAsync(result);

        await NotifyAsync(result, H["Moved {0} from {1} to {2}.", model.Quantity, from, to]);

        return RedirectToAction(nameof(Item), new { stockItemId });
    }

    [HttpPost]
    [Admin("inventory/{stockItemId}/rebuild", "InventoryRebuild")]
    public async Task<IActionResult> Rebuild(string stockItemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        var result = await CommitAsync(await _inventory.RebuildAsync(stockItemId, HttpContext.RequestAborted));

        await NotifyAsync(result, H["Recounted from the history: {0} on hand.", result.Item?.OnHand]);

        return RedirectToAction(nameof(Item), new { stockItemId });
    }

    private async Task<StockItemViewModel> BuildItemAsync(StockItem item, PagerParameters pagerParameters)
    {
        var pager = new Pager(pagerParameters, _pagerOptions);
        var (count, movements) = await _movements.PageAsync(item.ItemId, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        var holds = await _session.Query<StockReservation, StockReservationIndex>(index =>
                index.StockItemId == item.ItemId && index.Status == StockReservationStatus.Held,
                collection: InventoryConstants.CollectionName)
            .OrderBy(index => index.ExpiresUtc)
            .Take(50)
            .ListAsync(HttpContext.RequestAborted);

        var listed = await _locations.GetLocationsAsync();
        var locations = listed.ToList();

        // A location no longer listed still shows while it holds units of the item, so they can be moved.
        item.NormalizeLevels();

        foreach (var level in item.Levels.Where(level => level.OnHand != 0 || level.Reserved != 0))
        {
            if (!locations.Any(location => location.LocationId == level.LocationId))
            {
                locations.Add(new InventoryLocation { LocationId = level.LocationId, Name = S["Removed location"] });
            }
        }

        return new StockItemViewModel
        {
            Item = item,
            Locations = locations,
            ListedLocations = listed,
            Movements = movements,
            Holds = holds.ToArray(),
            MovementCount = count,
            WaitingAlerts = HttpContext.RequestServices.GetService(typeof(StockAlertService)) is StockAlertService alerts ? await alerts.CountWaitingAsync(item.ItemId, HttpContext.RequestAborted) : null,
            CanManage = await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory),
            Pager = await _shapeFactory.PagerAsync(pager, count, new RouteData(new RouteValueDictionary { { "stockItemId", item.ItemId } })),
        };
    }

    // A location that's listed, or the default one: a removed location only gives units away, by a transfer.
    private async Task<string> KnownLocationAsync(string locationId)
        => !string.IsNullOrEmpty(locationId) && (await _locations.GetLocationsAsync()).Any(location => location.LocationId == locationId)
            ? locationId
            : InventoryConstants.DefaultLocationId;

    // The change is committed here, so a checkout that changed the same stock at the same moment can't be overwritten:
    // the later of the two fails and is asked to try again.
    private async Task<StockChangeResult> CommitAsync(StockChangeResult result)
    {
        if (!result.Succeeded)
        {
            return result;
        }

        try
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);

            return result;
        }
        catch (ConcurrencyException)
        {
            await _session.CancelAsync();

            return StockChangeResult.Fail(StockChangeError.Busy);
        }
    }

    private async Task NotifyAsync(StockChangeResult result, LocalizedHtmlString success)
    {
        switch (result.Error)
        {
            case StockChangeError.None:
                await _notifier.SuccessAsync(success);
                break;
            case StockChangeError.NotFound:
                await _notifier.ErrorAsync(H["That stock item no longer exists."]);
                break;
            case StockChangeError.InvalidQuantity:
                await _notifier.ErrorAsync(H["Enter a quantity: more than zero for stock received, and not zero for an adjustment. A count can't be negative."]);
                break;
            default:
                await _notifier.WarningAsync(H["Someone else is changing this stock right now. Try again in a moment."]);
                break;
        }
    }

    private StockActor CurrentActor()
        => new(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);

    private static RouteValueDictionary FilterRouteValues(InventoryIndexOptions options, PagerParameters pagerParameters)
    {
        var values = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(options?.Search))
        {
            values.Add(OptionsSearch, options.Search);
        }

        if (options is not null && options.Filter != StockItemFilter.All)
        {
            values.Add(OptionsFilter, options.Filter);
        }

        if (pagerParameters?.PageSize is int pageSize)
        {
            values.Add("pageSize", pageSize);
        }

        return values;
    }
}
