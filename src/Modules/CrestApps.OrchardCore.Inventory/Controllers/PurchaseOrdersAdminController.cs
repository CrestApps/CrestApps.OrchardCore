using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Endpoints;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Products.Core.Services;
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
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using YesSql;

namespace CrestApps.OrchardCore.Inventory.Controllers;

/// <summary>
/// Commerce › Purchase Orders: stock ordered from suppliers, and received at a location when it arrives.
/// </summary>
[Feature(InventoryConstants.Features.PurchaseOrders)]
public sealed class PurchaseOrdersAdminController : Controller
{
    private readonly IPurchaseOrderStore _orders;
    private readonly IStockItemStore _items;
    private readonly IInventoryLocationService _locations;
    private readonly PurchaseOrderReceiver _receiver;
    private readonly IProductCurrencyProvider _currencyProvider;
    private readonly INumberSequence _numberSequence;
    private readonly IClock _clock;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;
    private readonly ISession _session;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public PurchaseOrdersAdminController(
        IPurchaseOrderStore orders,
        IStockItemStore items,
        IInventoryLocationService locations,
        PurchaseOrderReceiver receiver,
        IProductCurrencyProvider currencyProvider,
        INumberSequence numberSequence,
        IClock clock,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        INotifier notifier,
        ISession session,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<PurchaseOrdersAdminController> stringLocalizer,
        IHtmlLocalizer<PurchaseOrdersAdminController> htmlLocalizer)
    {
        _orders = orders;
        _items = items;
        _locations = locations;
        _receiver = receiver;
        _currencyProvider = currencyProvider;
        _numberSequence = numberSequence;
        _clock = clock;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        _session = session;
        _pagerOptions = pagerOptions.Value;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [Admin("purchase-orders", "PurchaseOrdersIndex")]
    public async Task<IActionResult> Index(PurchaseOrderIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ViewInventory))
        {
            return Forbid();
        }

        options ??= new PurchaseOrderIndexOptions();

        var pager = new Pager(pagerParameters, _pagerOptions);
        var query = new PurchaseOrderQuery { Search = options.Search };

        if (string.Equals(options.Show, "all", StringComparison.OrdinalIgnoreCase))
        {
            query.All = true;
        }
        else if (Enum.TryParse<PurchaseOrderStatus>(options.Show, ignoreCase: true, out var status))
        {
            query.Status = status;
        }

        var (count, orders) = await _orders.PageAsync(query, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        options.Filters =
        [
            new SelectListItem(S["Open"], string.Empty, string.IsNullOrEmpty(options.Show)),
            new SelectListItem(S["Drafts"], nameof(PurchaseOrderStatus.Draft), options.Show == nameof(PurchaseOrderStatus.Draft)),
            new SelectListItem(S["Ordered"], nameof(PurchaseOrderStatus.Ordered), options.Show == nameof(PurchaseOrderStatus.Ordered)),
            new SelectListItem(S["Partly received"], nameof(PurchaseOrderStatus.PartiallyReceived), options.Show == nameof(PurchaseOrderStatus.PartiallyReceived)),
            new SelectListItem(S["Received"], nameof(PurchaseOrderStatus.Received), options.Show == nameof(PurchaseOrderStatus.Received)),
            new SelectListItem(S["Canceled"], nameof(PurchaseOrderStatus.Canceled), options.Show == nameof(PurchaseOrderStatus.Canceled)),
            new SelectListItem(S["All"], "all", options.Show == "all"),
        ];

        var routeData = new RouteData();

        foreach (var (key, value) in FilterRouteValues(options, null))
        {
            routeData.Values.TryAdd(key, value);
        }

        var pagerShape = await _shapeFactory.PagerAsync(pager, count, routeData);

        return View(new PurchaseOrderIndexViewModel
        {
            Options = options,
            CanManage = await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory),
            List = new AdminListContext<PurchaseOrder>
            {
                Source = orders,
                Pager = pagerShape,
                ToolbarActions = await _shapeFactory.CreateAdminListFiltersAsync("PurchaseOrders", "purchase-orders-filter", new AdminListFilter("Options.Show", options.Filters, S["Show"], S["Show"])),
            },
        });
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("purchase-orders", "PurchaseOrdersIndex")]
    public async Task<IActionResult> IndexFilterPost(PurchaseOrderIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ViewInventory))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), FilterRouteValues(options, pagerParameters));
    }

    [Admin("purchase-orders/create", "PurchaseOrdersCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        var model = new PurchaseOrderEditViewModel { StartWithLowStock = true };

        await PopulateAsync(model, order: null);

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("purchase-orders/create", "PurchaseOrdersCreate")]
    public async Task<IActionResult> CreatePost(PurchaseOrderEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        var now = _clock.UtcNow;
        var order = new PurchaseOrder
        {
            ItemId = UniqueId.GenerateId(),
            Number = await PurchaseOrderRules.NewNumberAsync(_numberSequence, now, HttpContext.RequestAborted),
            Status = PurchaseOrderStatus.Draft,
            CreatedUtc = now,
            ModifiedUtc = now,
            OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Author = User.Identity?.Name,
        };

        await ApplyDetailsAsync(order, model);

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model, order: null);

            return View(model);
        }

        if (model.StartWithLowStock)
        {
            order.Lines = await PurchaseOrderRules.LowStockLinesAsync(_items, order.LocationId, HttpContext.RequestAborted);
        }

        await _orders.CreateAsync(order, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(order.Lines.Count > 0
            ? H["Purchase order {0} started with the {1} items that are low on stock. Check the quantities, then mark it ordered.", order.Number, order.Lines.Count]
            : H["Purchase order {0} started. Add the items to order.", order.Number]);

        return RedirectToAction(nameof(Edit), new { purchaseOrderId = order.ItemId });
    }

    [Admin("purchase-orders/{purchaseOrderId}", "PurchaseOrdersEdit")]
    public async Task<IActionResult> Edit(string purchaseOrderId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ViewInventory))
        {
            return Forbid();
        }

        var order = await FindAsync(purchaseOrderId);

        if (order is null)
        {
            return NotFound();
        }

        var model = new PurchaseOrderEditViewModel();

        await PopulateAsync(model, order);

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("purchase-orders/{purchaseOrderId}", "PurchaseOrdersEdit")]
    public async Task<IActionResult> EditPost(string purchaseOrderId, PurchaseOrderEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        var order = await FindAsync(purchaseOrderId);

        if (order is null)
        {
            return NotFound();
        }

        if (!PurchaseOrderRules.CanChange(order))
        {
            await _notifier.WarningAsync(H["A purchase order that's received or canceled can't be changed."]);

            return RedirectToAction(nameof(Edit), new { purchaseOrderId });
        }

        await ApplyDetailsAsync(order, model);
        await ApplyLinesAsync(order, model);

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model, order, keepPosted: true);

            return View(model);
        }

        order.ModifiedUtc = _clock.UtcNow;
        await _orders.UpdateAsync(order, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["Purchase order {0} saved.", order.Number]);

        return RedirectToAction(nameof(Edit), new { purchaseOrderId });
    }

    [HttpPost]
    [Admin("purchase-orders/{purchaseOrderId}/ordered", "PurchaseOrdersMarkOrdered")]
    public async Task<IActionResult> MarkOrdered(string purchaseOrderId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        var order = await FindAsync(purchaseOrderId);

        if (order is null)
        {
            return NotFound();
        }

        if (!order.IsDraft || order.Lines.Count == 0)
        {
            await _notifier.WarningAsync(order.Lines.Count == 0 ? H["Add the items to order first."] : H["This purchase order was already ordered."]);

            return RedirectToAction(nameof(Edit), new { purchaseOrderId });
        }

        order.Status = PurchaseOrderStatus.Ordered;
        order.OrderedUtc = order.ModifiedUtc = _clock.UtcNow;
        await _orders.UpdateAsync(order, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["Purchase order {0} is marked ordered. Receive its items here when they arrive.", order.Number]);

        return RedirectToAction(nameof(Edit), new { purchaseOrderId });
    }

    [HttpPost]
    [Admin("purchase-orders/{purchaseOrderId}/receive", "PurchaseOrdersReceive")]
    public async Task<IActionResult> Receive(string purchaseOrderId, PurchaseOrderEditViewModel model, bool all = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        var order = await FindAsync(purchaseOrderId);

        if (order is null)
        {
            return NotFound();
        }

        var quantities = all
            ? order.Lines.ToDictionary(line => line.LineId, line => line.Outstanding)
            : (model?.Lines ?? []).Where(line => !string.IsNullOrEmpty(line?.LineId) && line.Receive > 0)
                .GroupBy(line => line.LineId)
                .ToDictionary(group => group.Key, group => Math.Min(PurchaseOrderRules.MaxQuantity, group.Sum(line => line.Receive.Value)));

        var received = await _receiver.ReceiveAsync(order, quantities, StockActor.ForUser(User), HttpContext.RequestAborted);

        if (received == 0)
        {
            await _notifier.WarningAsync(order.CanReceive ? H["Enter how many of each item arrived."] : H["Stock can be received once the purchase order is marked ordered."]);

            return RedirectToAction(nameof(Edit), new { purchaseOrderId });
        }

        try
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (ConcurrencyException)
        {
            await _notifier.ErrorAsync(H["The stock changed while you were receiving it. Nothing was recorded; try again."]);

            return RedirectToAction(nameof(Edit), new { purchaseOrderId });
        }

        var location = (await _locations.GetLocationsAsync()).FirstOrDefault(entry => entry.LocationId == order.LocationId)?.Name ?? order.LocationId;

        await _notifier.SuccessAsync(order.Status == PurchaseOrderStatus.Received
            ? H["Received {0} at {1}. Purchase order {2} is complete.", received, location, order.Number]
            : H["Received {0} at {1}. {2} still to come.", received, location, order.Ordered - order.Received]);

        return RedirectToAction(nameof(Edit), new { purchaseOrderId });
    }

    [HttpPost]
    [Admin("purchase-orders/{purchaseOrderId}/cancel", "PurchaseOrdersCancel")]
    public async Task<IActionResult> Cancel(string purchaseOrderId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        var order = await FindAsync(purchaseOrderId);

        if (order is null)
        {
            return NotFound();
        }

        if (!PurchaseOrderRules.CanChange(order))
        {
            return RedirectToAction(nameof(Edit), new { purchaseOrderId });
        }

        // What was already received stays in stock; only what's still to come is canceled.
        order.Status = PurchaseOrderStatus.Canceled;
        order.ModifiedUtc = _clock.UtcNow;
        await _orders.UpdateAsync(order, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["Purchase order {0} is canceled.", order.Number]);

        return RedirectToAction(nameof(Edit), new { purchaseOrderId });
    }

    [HttpPost]
    [Admin("purchase-orders/{purchaseOrderId}/delete", "PurchaseOrdersDelete")]
    public async Task<IActionResult> Delete(string purchaseOrderId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory))
        {
            return Forbid();
        }

        var order = await FindAsync(purchaseOrderId);

        if (order is null)
        {
            return NotFound();
        }

        // An order that was sent to a supplier is a record of it: it can be canceled, not deleted.
        if (!order.IsDraft)
        {
            await _notifier.WarningAsync(H["Only a draft can be deleted. Cancel the purchase order instead."]);

            return RedirectToAction(nameof(Edit), new { purchaseOrderId });
        }

        await _orders.DeleteAsync(order, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["Draft {0} deleted.", order.Number]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<PurchaseOrder> FindAsync(string purchaseOrderId)
        => string.IsNullOrEmpty(purchaseOrderId) ? null : await _orders.FindByIdAsync(purchaseOrderId, HttpContext.RequestAborted);

    private async Task ApplyDetailsAsync(PurchaseOrder order, PurchaseOrderEditViewModel model)
    {
        var supplierGiven = PurchaseOrderRules.ApplyDetails(order, new PurchaseOrderDetails
        {
            Supplier = model.Supplier,
            SupplierEmail = model.SupplierEmail,
            LocationId = model.LocationId,
            Currency = model.Currency,
            ExpectedDate = model.ExpectedDate,
            Note = model.Note,
        }, await _locations.GetLocationsAsync());

        if (!supplierGiven)
        {
            ModelState.AddModelError(nameof(model.Supplier), S["Enter who the stock is ordered from."]);
        }
    }

    private async Task ApplyLinesAsync(PurchaseOrder order, PurchaseOrderEditViewModel model)
    {
        var problems = await PurchaseOrderRules.ApplyLinesAsync(
            order,
            (model.Lines ?? []).Where(posted => posted is not null).Select(posted => new PurchaseOrderLineChange(posted.LineId, posted.Ordered, posted.UnitCost, posted.Remove)),
            model.AddStockItemIds,
            _items,
            HttpContext.RequestAborted);

        foreach (var problem in problems)
        {
            ModelState.AddModelError(nameof(model.Lines), problem.Kind == PurchaseOrderLineProblemKind.Quantity
                ? S["Enter how many of {0} to order, or remove it.", problem.Line.Title]
                : S["A cost can't be negative."]);
        }
    }

    private async Task PopulateAsync(PurchaseOrderEditViewModel model, PurchaseOrder order, bool keepPosted = false)
    {
        var locations = await _locations.GetLocationsAsync();
        var currencies = await _currencyProvider.GetCurrenciesAsync(HttpContext.RequestAborted);

        if (order is not null && !keepPosted)
        {
            model.PurchaseOrderId = order.ItemId;
            model.Supplier = order.Supplier;
            model.SupplierEmail = order.SupplierEmail;
            model.LocationId = order.LocationId;
            model.Currency = order.Currency;
            model.ExpectedDate = order.ExpectedUtc?.Date;
            model.Note = order.Note;
        }

        model.Order = order;
        model.PurchaseOrderId ??= order?.ItemId;
        model.LocationId ??= locations[0].LocationId;
        model.Currency ??= currencies.Count > 0 ? currencies[0].CurrencyCode : null;
        model.Locations = locations.Select(location => new SelectListItem(location.Name, location.LocationId, location.LocationId == model.LocationId)).ToArray();
        model.LocationName = locations.FirstOrDefault(location => location.LocationId == (order?.LocationId ?? model.LocationId))?.Name ?? order?.LocationId;
        model.Currencies = currencies.Select(currency => new SelectListItem($"{currency.CurrencyCode} ({currency.DisplayName})", currency.CurrencyCode, currency.CurrencyCode == model.Currency)).ToArray();
        model.Suppliers = await _orders.ListSuppliersAsync(HttpContext.RequestAborted);
        model.CanManage = await _authorizationService.AuthorizeAsync(User, InventoryPermissions.ManageInventory);
        model.SearchEndpoint = Url.RouteUrl(StockItemSearchEndpoints.RouteName);
    }

    private static RouteValueDictionary FilterRouteValues(PurchaseOrderIndexOptions options, PagerParameters pagerParameters)
    {
        var values = new RouteValueDictionary();

        if (!string.IsNullOrWhiteSpace(options?.Search))
        {
            values["Options.Search"] = options.Search;
        }

        if (!string.IsNullOrWhiteSpace(options?.Show))
        {
            values["Options.Show"] = options.Show;
        }

        if (pagerParameters?.PageSize is { } pageSize)
        {
            values["pageSize"] = pageSize;
        }

        return values;
    }
}
