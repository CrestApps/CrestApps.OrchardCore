using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Endpoints;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Email;
using YesSql;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// Orders the store puts together for a buyer and emails them to pay: made and changed here, sent from the order's
/// page.
/// </summary>
public sealed class DraftOrdersAdminController : Controller
{
    private const int MaxLines = 100;

    private readonly IOrderStore _orderStore;
    private readonly DraftOrderService _drafts;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ISession _session;

    internal readonly IHtmlLocalizer H;

    public DraftOrdersAdminController(
        IOrderStore orderStore,
        DraftOrderService drafts,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        INotifier notifier,
        ISession session,
        IHtmlLocalizer<DraftOrdersAdminController> htmlLocalizer)
    {
        _orderStore = orderStore;
        _drafts = drafts;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _session = session;
        H = htmlLocalizer;
    }

    [Admin("orders/drafts/create", "OrdersCreate")]
    public async Task<IActionResult> Create(string addsToOrderId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        var model = new DraftOrderEditViewModel { AddsToOrderId = addsToOrderId };

        // Items added to a paid order go on a new order for the same buyer.
        if (!string.IsNullOrEmpty(addsToOrderId) && await _orderStore.FindByIdAsync(addsToOrderId, HttpContext.RequestAborted) is { } original)
        {
            model.CustomerEmail = original.CustomerEmail;
            model.CustomerName = original.CustomerName;
            model.AddsToOrderNumber = original.OrderNumber;
        }

        await PopulateAsync(model, order: null, pricing: null);

        return View("Edit", model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("orders/drafts/create", "OrdersCreate")]
    public async Task<IActionResult> CreatePost(DraftOrderEditViewModel model, string submit)
        => await SaveAsync(order: null, model, submit);

    [Admin("orders/{orderId}/edit", "OrdersEdit")]
    public async Task<IActionResult> Edit(string orderId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        var order = await FindDraftAsync(orderId);

        if (order is null)
        {
            return RedirectToAction("Detail", "OrdersAdmin", new { orderId });
        }

        var model = new DraftOrderEditViewModel
        {
            CustomerEmail = order.CustomerEmail,
            CustomerName = order.CustomerName,
            Note = order.Invoice.Note,
            DueDate = order.Invoice.DueUtc?.Date,
            AddsToOrderId = order.Invoice.AddsToOrderId,
            AddsToOrderNumber = order.Invoice.AddsToOrderNumber,
            Lines = [.. order.Lines.Select(line => new DraftOrderLineViewModel
            {
                LineId = line.LineId,
                ContentItemId = line.ContentItemId,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                UnitPrice = line.QuotedUnitPrice?.ToString(CultureInfo.InvariantCulture),
                CatalogUnitPrice = line.QuotedUnitPrice is null ? line.UnitPrice : null,
                Title = line.Title,
                LineTotal = line.LineTotal,
            })],
        };

        await PopulateAsync(model, order, pricing: null);

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("orders/{orderId}/edit", "OrdersEdit")]
    public async Task<IActionResult> EditPost(string orderId, DraftOrderEditViewModel model, string submit)
    {
        var order = await FindDraftAsync(orderId);

        return order is null ? RedirectToAction("Detail", "OrdersAdmin", new { orderId }) : await SaveAsync(order, model, submit);
    }

    [HttpPost]
    [Admin("orders/{orderId}/send-invoice", "OrdersSendInvoice")]
    public async Task<IActionResult> SendInvoice(string orderId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted);

        if (order?.Invoice is null)
        {
            return NotFound();
        }

        if (await _drafts.SendInvoiceAsync(order, User.Identity?.Name, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(order.Quote is null ? H["Invoice emailed to {0}.", order.CustomerEmail] : H["Quote emailed to {0}.", order.CustomerEmail]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The invoice couldn't be emailed. Check the email settings and the site's base URL."]);
        }

        return RedirectToAction("Detail", "OrdersAdmin", new { orderId });
    }

    // "Update" re-prices the draft and stays; "Save" saves it and opens the order, where it's sent.
    private async Task<IActionResult> SaveAsync(Order order, DraftOrderEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageOrders))
        {
            return Forbid();
        }

        var input = ToInput(model);

        if (submit == "update")
        {
            var pricing = await _drafts.PriceAsync(input, order?.CartId, order?.Currency, order, HttpContext.RequestAborted);

            model.Lines = ToLines(input);
            await PopulateAsync(model, order, pricing);

            return View("Edit", model);
        }

        var (saved, problems) = await _drafts.SaveAsync(order, input, User.Identity?.Name, HttpContext.RequestAborted);

        if (problems.Count > 0)
        {
            await _session.CancelAsync();

            model.Lines = ToLines(input);
            model.Problems = problems;
            await PopulateAsync(model, order, await _drafts.PriceAsync(input, order?.CartId, order?.Currency, order, HttpContext.RequestAborted));

            return View("Edit", model);
        }

        try
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (ConcurrencyException)
        {
            await _notifier.ErrorAsync(H["The stock changed at the same moment. Nothing was saved; try again."]);

            return order is null ? RedirectToAction(nameof(Create)) : RedirectToAction(nameof(Edit), new { orderId = order.ItemId });
        }

        await _notifier.SuccessAsync(order is null
            ? H["Order {0} saved as a draft. Send the invoice when it's ready.", saved.OrderNumber]
            : H["Order {0} saved.", saved.OrderNumber]);

        return RedirectToAction("Detail", "OrdersAdmin", new { orderId = saved.ItemId });
    }

    private static DraftOrderInput ToInput(DraftOrderEditViewModel model)
    {
        var lines = (model.Lines ?? [])
            .Where(line => line is not null && !line.Remove && !string.IsNullOrEmpty(line.ContentItemId))
            .Select(line => new DraftOrderLineInput(
                string.IsNullOrEmpty(line.LineId) ? UniqueId.GenerateId() : line.LineId,
                line.ContentItemId,
                line.VariantId,
                Math.Clamp(line.Quantity, 0, 10_000),
                ParsePrice(line.UnitPrice)))
            .ToList();

        foreach (var productId in (model.AddProductIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (lines.Count < MaxLines)
            {
                // An id now, so the line keeps it from one pricing to the next.
                lines.Add(new DraftOrderLineInput(UniqueId.GenerateId(), productId, null, 1));
            }
        }

        return new DraftOrderInput
        {
            CustomerEmail = model.CustomerEmail,
            CustomerName = model.CustomerName,
            Note = model.Note,
            DueUtc = model.DueDate is { } due ? DateTime.SpecifyKind(due.Date.AddHours(12), DateTimeKind.Utc) : null,
            AddsToOrderId = model.AddsToOrderId,
            Lines = lines,
        };
    }

    private static List<DraftOrderLineViewModel> ToLines(DraftOrderInput input)
        => [.. input.Lines.Select(line => new DraftOrderLineViewModel
        {
            LineId = line.LineId,
            ContentItemId = line.ContentItemId,
            VariantId = line.VariantId,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice?.ToString(CultureInfo.InvariantCulture),
        })];

    private static decimal? ParsePrice(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price is >= 0 and < 100_000_000
            ? price
            : null;

    private async Task PopulateAsync(DraftOrderEditViewModel model, Order order, Carts.Core.Models.CartPricing pricing)
    {
        var pathBase = HttpContext.Request.PathBase.Value ?? string.Empty;

        model.Order = order;
        model.ProductSearchEndpoint = $"{pathBase}/{CouponItemSearchEndpoints.ProductsPath}";
        model.CanEmail = HttpContext.RequestServices.GetService<IEmailService>() is not null;
        model.AddProductIds = [];

        if (string.IsNullOrEmpty(model.AddsToOrderNumber) && !string.IsNullOrEmpty(model.AddsToOrderId) &&
            await _orderStore.FindByIdAsync(model.AddsToOrderId, HttpContext.RequestAborted) is { } original)
        {
            model.AddsToOrderNumber = original.OrderNumber;
        }

        // Each line's product, for its title and, when it's sold in variants, the variant to choose.
        var items = (await _contentManager.GetAsync(model.Lines.Select(line => line.ContentItemId).Distinct(), VersionOptions.Published))
            .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);

        foreach (var line in model.Lines)
        {
            var priced = pricing?.Lines.FirstOrDefault(entry => entry.Line.LineId == line.LineId);

            if (items.TryGetValue(line.ContentItemId, out var item))
            {
                line.Title ??= item.DisplayText;

                if (item.TryGet<ProductVariantsPart>(out var variants) && variants.Variants.Count > 0)
                {
                    line.Variants = [.. variants.GetAvailable().Select(variant => new SelectListItem(
                        string.Join(" / ", variant.Options.Values) + (string.IsNullOrEmpty(variant.Sku) ? string.Empty : $" ({variant.Sku})"),
                        variant.VariantId,
                        variant.VariantId == line.VariantId))];
                }
            }

            if (priced is not null)
            {
                line.Title = priced.Title ?? line.Title;
                line.CatalogUnitPrice = priced.UnitPrice;
                line.LineTotal = ParsePrice(line.UnitPrice) is { } price ? price * line.Quantity : priced.LineTotal;
                line.Problem = priced.Problem?.Value;
            }
        }

        if (pricing is not null)
        {
            model.Subtotal = model.Lines.Sum(line => line.LineTotal ?? 0);
            model.Currency = pricing.Currency;
        }
        else if (order is not null)
        {
            model.Subtotal = order.Subtotal;
            model.Currency = order.Currency;
        }
    }

    private async Task<Order> FindDraftAsync(string orderId)
        => string.IsNullOrEmpty(orderId) || await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted) is not { Invoice: not null, Status: OrderStatus.Draft } order
            ? null
            : order;
}
