using System.Security.Claims;
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
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Inventory.Controllers;

/// <summary>
/// Commerce > Inventory: what is on hand, what is low, and the history of every change.
/// </summary>
public sealed class InventoryAdminController : Controller
{
    private const string OptionsSearch = "Options.Search";
    private const string OptionsFilter = "Options.Filter";

    private readonly IStockItemStore _items;
    private readonly IStockMovementStore _movements;
    private readonly IInventoryService _inventory;
    private readonly InventorySyncService _sync;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public InventoryAdminController(
        IStockItemStore items,
        IStockMovementStore movements,
        IInventoryService inventory,
        InventorySyncService sync,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        INotifier notifier,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<InventoryAdminController> stringLocalizer,
        IHtmlLocalizer<InventoryAdminController> htmlLocalizer)
    {
        _items = items;
        _movements = movements;
        _inventory = inventory;
        _sync = sync;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
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

        return View(new InventoryIndexViewModel
        {
            Options = options,
            Items = items,
            TotalCount = count,
            CanManage = await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory),
            Pager = await _shapeFactory.PagerAsync(pager, count, routeData),
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

        var result = await _inventory.AdjustAsync(stockItemId, model.Kind, model.Quantity, model.Reason, CurrentActor(), HttpContext.RequestAborted);

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

        var result = await _inventory.SetCountAsync(stockItemId, count, model.Reason, CurrentActor(), HttpContext.RequestAborted);

        await NotifyAsync(result, H["Counted {0}. The difference was {1}.", count, result.Movement?.Quantity]);

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

        var result = await _inventory.RebuildAsync(stockItemId, HttpContext.RequestAborted);

        await NotifyAsync(result, H["Recounted from the history: {0} on hand.", result.Item?.OnHand]);

        return RedirectToAction(nameof(Item), new { stockItemId });
    }

    private async Task<StockItemViewModel> BuildItemAsync(StockItem item, PagerParameters pagerParameters)
    {
        var pager = new Pager(pagerParameters, _pagerOptions);
        var (count, movements) = await _movements.PageAsync(item.ItemId, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        return new StockItemViewModel
        {
            Item = item,
            Movements = movements,
            MovementCount = count,
            CanManage = await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory),
            Pager = await _shapeFactory.PagerAsync(pager, count, new RouteData(new RouteValueDictionary { { "stockItemId", item.ItemId } })),
        };
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
