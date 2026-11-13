using System.Globalization;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.PointOfSale.ViewModels;
using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.PointOfSale.Controllers;

/// <summary>
/// The web register: staff ring up sales in the browser at a register they may sell at, by department keys and by SKU,
/// take cash or a card on the counter terminal, and open, count and close the shift, without a till app.
/// </summary>
/// <remarks>
/// It calls the same services the point-of-sale API does, so a sale here is the same order and checkout as one from a
/// till app. Its own prefix keeps the registers controller's <c>point-of-sale/{action}</c> from taking its routes.
/// </remarks>
[Feature(PosConstants.WebRegisterFeature)]
[Admin("pos-register/{action}/{registerId?}", "PosWebRegister{action}")]
public sealed class WebRegisterController : Controller
{
    private readonly IRegisterStore _registers;
    private readonly IRegisterShiftStore _shifts;
    private readonly IPosDepartmentStore _departments;
    private readonly PosShiftService _shiftService;
    private readonly PosSaleService _sales;
    private readonly IOrderStore _orders;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly ICheckoutEngine _engine;
    private readonly ICartService _cartService;
    private readonly IEnumerable<ICheckoutPaymentProvider> _paymentProviders;
    private readonly global::YesSql.ISession _session;
    private readonly ILocalClock _localClock;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public WebRegisterController(
        IRegisterStore registers,
        IRegisterShiftStore shifts,
        IPosDepartmentStore departments,
        PosShiftService shiftService,
        PosSaleService sales,
        IOrderStore orders,
        ICheckoutSessionStore sessionStore,
        ICheckoutEngine engine,
        ICartService cartService,
        IEnumerable<ICheckoutPaymentProvider> paymentProviders,
        global::YesSql.ISession session,
        ILocalClock localClock,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IStringLocalizer<WebRegisterController> stringLocalizer)
    {
        _registers = registers;
        _shifts = shifts;
        _departments = departments;
        _shiftService = shiftService;
        _sales = sales;
        _orders = orders;
        _sessionStore = sessionStore;
        _engine = engine;
        _cartService = cartService;
        _paymentProviders = paymentProviders;
        _session = session;
        _localClock = localClock;
        _siteService = siteService;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the registers the staff member may sell at, or opens the only one.
    /// </summary>
    [Admin("pos-register", "PosWebRegisterIndex")]
    public async Task<IActionResult> Index()
    {
        var registers = await _shiftService.ListForAsync(User, HttpContext.RequestAborted);

        if (registers.Count == 0 && !await _authorizationService.AuthorizeAsync(User, PosPermissions.SellAtRegister))
        {
            return Forbid();
        }

        if (registers.Count == 1)
        {
            return RedirectToAction(nameof(Till), new { registerId = registers[0].ItemId });
        }

        var choices = new List<WebRegisterChoice>();

        foreach (var register in registers)
        {
            choices.Add(new WebRegisterChoice(register, await _shifts.FindOpenAsync(register.ItemId, HttpContext.RequestAborted)));
        }

        return View(new WebRegisterIndexViewModel { Registers = choices });
    }

    /// <summary>
    /// The register page itself.
    /// </summary>
    public async Task<IActionResult> Till(string registerId)
    {
        if (await FindRegisterAsync(registerId) is not { } register)
        {
            return NotFound();
        }

        var shift = await _shifts.FindOpenAsync(register.ItemId, HttpContext.RequestAborted);

        return View(new WebRegisterViewModel
        {
            Register = register,
            Currency = register.Currency ?? shift?.Currency ?? (await GetStoreCurrencyAsync()),
            Keys = await GetKeysAsync(register),
            CanTakeCards = _paymentProviders.Any(provider => provider.Key == PosConstants.CounterCardProviderKey),
        });
    }

    [HttpGet]
    public async Task<IActionResult> Shift(string registerId)
    {
        if (await FindRegisterAsync(registerId) is not { } register)
        {
            return NotFound();
        }

        var shift = await _shifts.FindOpenAsync(register.ItemId, HttpContext.RequestAborted);

        return Json(new { shift = shift is null ? null : await ToJsonAsync(shift) });
    }

    [HttpPost]
    public async Task<IActionResult> OpenShift(string registerId, [FromBody] WebRegisterOpenShiftRequest request)
    {
        if (await FindRegisterAsync(registerId) is not { } register)
        {
            return NotFound();
        }

        if (request?.OpeningFloat is < 0m)
        {
            return Refuse(S["Enter the cash put in the drawer, or nothing."]);
        }

        if (await _shiftService.OpenAsync(register, request?.OpeningFloat ?? 0m, User, HttpContext.RequestAborted) is not { } shift)
        {
            return Refuse(S["The register already has an open shift. Reload the page to use it."]);
        }

        return Json(new { shift = await ToJsonAsync(shift) });
    }

    [HttpPost]
    public async Task<IActionResult> MoveCash(string registerId, [FromBody] WebRegisterCashRequest request)
    {
        if (await FindRegisterAsync(registerId) is not { } register)
        {
            return NotFound();
        }

        if (request?.Amount is not { } amount || amount == 0m)
        {
            return Refuse(S["Enter the cash put in, or taken out."]);
        }

        if (await _shifts.FindOpenAsync(register.ItemId, HttpContext.RequestAborted) is not { } shift ||
            !await _shiftService.MoveCashAsync(shift, amount, request.Reason, User, HttpContext.RequestAborted))
        {
            return Refuse(S["Open a shift at the register first."]);
        }

        return Json(new { shift = await ToJsonAsync(shift) });
    }

    [HttpPost]
    public async Task<IActionResult> CloseShift(string registerId, [FromBody] WebRegisterCloseShiftRequest request)
    {
        if (await FindRegisterAsync(registerId) is not { } register)
        {
            return NotFound();
        }

        if (request?.CountedCash is not { } counted || counted < 0m)
        {
            return Refuse(S["Enter the cash counted in the drawer."]);
        }

        if (await _shifts.FindOpenAsync(register.ItemId, HttpContext.RequestAborted) is not { } shift ||
            !await _shiftService.CloseAsync(shift, counted, request.Note, User, HttpContext.RequestAborted))
        {
            return Refuse(S["The shift is already closed."]);
        }

        return Json(new { shift = await ToJsonAsync(shift) });
    }

    /// <summary>
    /// Finds a product by its SKU, or one of its variants by theirs, with its price at the register.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Product(string registerId, string sku)
    {
        if (await FindRegisterAsync(registerId) is not { } register)
        {
            return NotFound();
        }

        sku = sku?.Trim();

        if (string.IsNullOrEmpty(sku) || sku.Length > ProductSkuIndex.MaxSkuLength)
        {
            return Refuse(S["Scan or enter a SKU."]);
        }

        var found = await _session.QueryIndex<ProductSkuIndex>(index => index.Published && index.Sku == sku).FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (found is null)
        {
            return Refuse(S["No product has the SKU {0}.", sku]);
        }

        var cart = new Cart
        {
            Currency = register.Currency,
            Lines = [new CartLine { ContentItemId = found.ContentItemId, VariantId = found.VariantId, Quantity = 1 }],
        };
        var line = (await _cartService.PriceAsync(cart, HttpContext.RequestAborted)).Lines.Single();

        if (line.Problem is not null)
        {
            return Refuse(line.Problem.Value);
        }

        return Json(new
        {
            productId = found.ContentItemId,
            variantId = found.VariantId,
            title = line.Title,
            sku,
            unitPrice = line.UnitPrice,
            currency = line.Price?.Currency ?? cart.Currency,
        });
    }

    /// <summary>
    /// Rings up the ticket in the open shift and returns what it comes to, with tax, for staff to take payment.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Sale(string registerId, [FromBody] WebRegisterSaleRequest request)
    {
        if (await FindRegisterAsync(registerId) is not { } register)
        {
            return NotFound();
        }

        if (request?.Lines is not { Count: > 0 })
        {
            return Refuse(S["Ring up at least one item."]);
        }

        if (await _shifts.FindOpenAsync(register.ItemId, HttpContext.RequestAborted) is not { } shift)
        {
            return Refuse(S["Open a shift at the register first."]);
        }

        var lines = request.Lines
            .Where(line => line is not null)
            .Select(line => new PosSaleLine
            {
                DepartmentId = string.IsNullOrEmpty(line.DepartmentId) ? null : line.DepartmentId,
                Amount = line.Amount,
                ProductId = string.IsNullOrEmpty(line.DepartmentId) ? line.ProductId : null,
                VariantId = string.IsNullOrEmpty(line.DepartmentId) ? line.VariantId : null,
                Quantity = line.Quantity,
            })
            .ToList();

        var result = await _sales.StartAsync(register, shift, lines, buyer: null, User, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return Refuse(string.Join(" ", result.Problems));
        }

        return Json(ToJson(result.Session, result.Order));
    }

    /// <summary>
    /// Pays a sale rung up here: cash handed over, then the card terminal for what's left.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Pay(string registerId, [FromBody] WebRegisterPayRequest request)
    {
        if (await FindRegisterAsync(registerId) is not { } register)
        {
            return NotFound();
        }

        if (request?.Cash is < 0m)
        {
            return Refuse(S["Enter the cash handed over."]);
        }

        if (await FindSaleAsync(register, request?.CheckoutId) is not { } session)
        {
            return Refuse(S["The sale is no longer open. Ring it up again."]);
        }

        var result = await _sales.PayAsync(session, request.Cash, request.Card, request.ApprovalCode, readerId: null, HttpContext.RequestAborted);

        if (result.Status == PosPaymentStatus.Failed)
        {
            return Refuse(result.Error ?? S["The payment didn't go through."]);
        }

        var order = result.Status == PosPaymentStatus.Completed ? await _orders.FindByIdAsync(session.ReferenceId, HttpContext.RequestAborted) : null;

        return Json(new
        {
            status = result.Status switch
            {
                PosPaymentStatus.Completed => "completed",
                PosPaymentStatus.Pending => "pending",
                _ => "due",
            },
            amountDue = result.AmountDue,
            change = result.Change,
            orderId = order?.ItemId,
            orderNumber = order?.OrderNumber,
        });
    }

    /// <summary>
    /// Voids a sale rung up here that hasn't been paid: its order is canceled and anything it held is let go.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Cancel(string registerId, [FromBody] WebRegisterCancelRequest request)
    {
        if (await FindRegisterAsync(registerId) is not { } register)
        {
            return NotFound();
        }

        if (await FindSaleAsync(register, request?.CheckoutId) is { } session)
        {
            await _engine.CancelAsync(session.SessionId, "Voided at the register", HttpContext.RequestAborted);
        }

        return Json(new { canceled = true });
    }

    /// <summary>
    /// A sale's receipt, to print: the store's branded receipt when Receipts is on, or a plain till receipt.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Receipt(string registerId, string orderId)
    {
        if (await FindRegisterAsync(registerId) is not { } register ||
            await _orders.FindByIdAsync(orderId, HttpContext.RequestAborted) is not { } order ||
            !OrderChannels.IsCounterSale(order) ||
            order.RegisterId != register.ItemId)
        {
            return NotFound();
        }

        if (await PosReceipts.RenderAsync(order, HttpContext.RequestServices) is { } html)
        {
            return Content(html, "text/html; charset=utf-8");
        }

        return View(new WebRegisterReceiptViewModel
        {
            Register = register,
            Order = order,
            SoldAt = (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(order.PaidUtc ?? order.CreatedUtc, DateTimeKind.Utc))).DateTime,
        });
    }

    // A register this staff member may sell at, or null.
    private async Task<Register> FindRegisterAsync(string registerId)
        => !string.IsNullOrEmpty(registerId) &&
            await _registers.FindByIdAsync(registerId, HttpContext.RequestAborted) is { } register &&
            await _shiftService.MaySellAtAsync(User, register)
                ? register
                : null;

    // An open sale of this register's open shift, or null: money goes into the open drawer only.
    private async Task<CheckoutSession> FindSaleAsync(Register register, string checkoutId)
    {
        if (string.IsNullOrEmpty(checkoutId) ||
            await CheckoutApi.FindOpenAsync(_sessionStore, checkoutId, HttpContext.RequestAborted) is not { } session ||
            !session.TryGet<CheckoutCounterSale>(out var counterSale) ||
            counterSale.RegisterId != register.ItemId)
        {
            return null;
        }

        return await _shifts.FindByIdAsync(counterSale.ShiftId, HttpContext.RequestAborted) is { Status: RegisterShiftStatus.Open }
            ? session
            : null;
    }

    // The keys the register shows: those it was given, or every department that's on, in their order.
    private async Task<IReadOnlyList<WebRegisterKey>> GetKeysAsync(Register register)
        => [.. PosDepartments.GetKeys(register, await _departments.ListOrderedAsync(HttpContext.RequestAborted))
            .Select(key => new WebRegisterKey(key.Department.ItemId, key.Label, key.Department.Color, key.PresetAmount, key.Department.MinimumAmount, key.Department.MaximumAmount))];

    private async Task<string> GetStoreCurrencyAsync()
        => (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

    private static ObjectResult Refuse(string detail)
        => new(new { error = detail }) { StatusCode = StatusCodes.Status409Conflict };

    private static object ToJson(CheckoutSession session, Order order)
    {
        session.TryGet<CheckoutInvoice>(out var invoice);

        return new
        {
            checkoutId = session.SessionId,
            orderId = order.ItemId,
            orderNumber = order.OrderNumber,
            currency = invoice?.Currency ?? order.Currency,
            subtotal = order.Subtotal,
            discount = invoice?.DiscountTotal ?? 0m,
            tax = invoice?.TaxAmount ?? 0m,
            total = invoice?.GrandTotal ?? order.Subtotal,
            amountDue = invoice?.AmountDue ?? order.Subtotal,
        };
    }

    private async Task<object> ToJsonAsync(RegisterShift shift)
    {
        var summary = shift.Summary ?? await _shiftService.SummarizeAsync(shift, HttpContext.RequestAborted);
        var expected = shift.ExpectedCash ?? shift.GetExpectedCash(summary.Cash);
        var movements = new List<object>();

        foreach (var movement in shift.CashMovements ?? [])
        {
            movements.Add(new { amount = movement.Amount, reason = movement.Reason, by = movement.ByName, at = await FormatLocalAsync(movement.Utc) });
        }

        return new
        {
            id = shift.ItemId,
            status = shift.Status == RegisterShiftStatus.Open ? "open" : "closed",
            currency = shift.Currency,
            openingFloat = shift.OpeningFloat,
            sales = summary.Sales,
            total = summary.Total,
            tax = summary.Tax,
            cash = summary.Cash,
            card = summary.Card,
            other = summary.Other,
            refunded = summary.Refunded,
            cashMovements = movements,
            movedCash = (shift.CashMovements ?? []).Sum(movement => movement.Amount),
            expectedCash = expected,
            countedCash = shift.CountedCash,
            difference = shift.CountedCash is { } counted ? counted - expected : (decimal?)null,
            departments = summary.Departments.Select(department => new { name = department.Name, quantity = department.Quantity, total = department.Total }),
            openedBy = shift.OpenedByName,
            openedAt = await FormatLocalAsync(shift.OpenedUtc),
            closedBy = shift.ClosedByName,
            closedAt = shift.ClosedUtc is { } closed ? await FormatLocalAsync(closed) : null,
        };
    }

    // A time as the store reads it, in the site's (or the user's) time zone.
    private async Task<string> FormatLocalAsync(DateTime utc)
        => (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc, DateTimeKind.Utc))).DateTime.ToString("g", CultureInfo.CurrentCulture);
}
