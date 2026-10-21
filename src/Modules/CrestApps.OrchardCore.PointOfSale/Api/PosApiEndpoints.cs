using System.Security.Claims;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.PointOfSale.Api;

/// <summary>
/// The point-of-sale API a till app calls: the registers staff may use, their shifts and cash, ringing up and paying
/// sales, receipts, and returns at the till.
/// </summary>
internal static class PosApiEndpoints
{
    public static IEndpointRouteBuilder MapPosApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(PosConstants.ApiTag);

        group.MapGet("pos/registers", ListRegistersAsync)
            .WithName("ListPosRegisters")
            .WithSummary("Lists the registers the caller may sell at, each with its open shift.");

        group.MapPost("pos/registers/{registerId}/shifts", OpenShiftAsync)
            .WithIdempotency()
            .WithName("OpenPosShift")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Opens a shift at a register with the cash put in its drawer.");

        group.MapGet("pos/shifts/{shiftId}", GetShiftAsync)
            .WithName("GetPosShift")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets a shift: what it's sold, how it was paid, and the cash its drawer should hold.");

        group.MapPost("pos/shifts/{shiftId}/cash", MoveCashAsync)
            .WithIdempotency()
            .WithName("MovePosShiftCash")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Records cash put in the drawer (a positive amount) or taken out (a negative one), with the reason.");

        group.MapPost("pos/shifts/{shiftId}/close", CloseShiftAsync)
            .WithName("ClosePosShift")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Closes a shift with the cash counted in its drawer, against what it should hold.");

        group.MapPost("pos/sales", StartSaleAsync)
            .WithIdempotency()
            .WithName("StartPosSale")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Rings up products at a register in its open shift, for a known customer or a walk-in buyer. Codes and gift cards go on the sale's checkout with the checkout endpoints.");

        group.MapPost("pos/sales/{checkoutId}/pay", PayAsync)
            .WithIdempotency()
            .WithName("PayPosSale")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Pays a sale: the cash handed over, then a card on the counter terminal or a Stripe reader for what's left; it completes once nothing is left to pay, and says the change. A reader's payment answers pending until the card is presented: pay again with nothing to ask.");

        group.MapGet("pos/sales/{orderId}/receipt", GetReceiptAsync)
            .WithName("GetPosReceipt")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets a sale's receipt as a printable HTML page, from the Receipts settings.");

        group.MapPost("pos/sales/{orderId}/receipt/email", EmailReceiptAsync)
            .WithName("EmailPosReceipt")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Emails a sale's receipt.");

        group.MapPost("pos/returns", ReturnAsync)
            .WithIdempotency()
            .WithName("PosReturn")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Refunds a sale at the till, all of it or an amount, putting its stock back at the register's store; cash given back comes out of the drawer.");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<ApiRegister>>> ListRegistersAsync(HttpContext httpContext, PosShiftService shifts, IRegisterShiftStore shiftStore)
    {
        var registers = new List<ApiRegister>();

        foreach (var register in await shifts.ListForAsync(httpContext.User, httpContext.RequestAborted))
        {
            var open = await shiftStore.FindOpenAsync(register.ItemId, httpContext.RequestAborted);

            registers.Add(new ApiRegister(register.ItemId, register.Name, register.LocationId, register.Currency, ApiAddress.From(register.Address), open?.ItemId));
        }

        return TypedResults.Ok<IReadOnlyList<ApiRegister>>(registers);
    }

    private static async Task<Results<Created<ApiShift>, ProblemHttpResult>> OpenShiftAsync(
        string registerId,
        ApiOpenShiftRequest request,
        HttpContext httpContext,
        IRegisterStore registers,
        PosShiftService shifts)
    {
        if (await registers.FindByIdAsync(registerId, httpContext.RequestAborted) is not { } register || !await shifts.MaySellAtAsync(httpContext.User, register))
        {
            return ApiProblems.NotFound();
        }

        if (await shifts.OpenAsync(register, request?.OpeningFloat ?? 0m, httpContext.User, httpContext.RequestAborted) is not { } shift)
        {
            return ApiProblems.Conflict(PosCodes.ShiftOpen, "The register already has an open shift. Close it first.");
        }

        return TypedResults.Created($"{httpContext.Request.PathBase}/{CommerceApi.RoutePrefix}/pos/shifts/{shift.ItemId}", await ToApiAsync(shift, shifts, httpContext));
    }

    private static async Task<Results<Ok<ApiShift>, ProblemHttpResult>> GetShiftAsync(string shiftId, HttpContext httpContext, PosShiftService shifts, IRegisterShiftStore shiftStore, IRegisterStore registers)
        => await FindShiftAsync(shiftId, httpContext, shiftStore, registers, shifts) is { } found
            ? TypedResults.Ok(await ToApiAsync(found.Shift, shifts, httpContext))
            : ApiProblems.NotFound();

    private static async Task<Results<Ok<ApiShift>, ValidationProblem, ProblemHttpResult>> MoveCashAsync(
        string shiftId,
        ApiCashMovementRequest request,
        HttpContext httpContext,
        PosShiftService shifts,
        IRegisterShiftStore shiftStore,
        IRegisterStore registers)
    {
        if (request?.Amount is not { } amount || amount == 0m)
        {
            return ApiProblems.Validation("amount", "Give the cash put in (positive) or taken out (negative).");
        }

        if (await FindShiftAsync(shiftId, httpContext, shiftStore, registers, shifts) is not { } found)
        {
            return ApiProblems.NotFound();
        }

        return await shifts.MoveCashAsync(found.Shift, amount, request.Reason, httpContext.User, httpContext.RequestAborted)
            ? TypedResults.Ok(await ToApiAsync(found.Shift, shifts, httpContext))
            : ApiProblems.Conflict(PosCodes.ShiftClosed, "The shift is closed.");
    }

    private static async Task<Results<Ok<ApiShift>, ValidationProblem, ProblemHttpResult>> CloseShiftAsync(
        string shiftId,
        ApiCloseShiftRequest request,
        HttpContext httpContext,
        PosShiftService shifts,
        IRegisterShiftStore shiftStore,
        IRegisterStore registers)
    {
        if (request?.CountedCash is not { } counted || counted < 0m)
        {
            return ApiProblems.Validation("countedCash", "Give the cash counted in the drawer.");
        }

        if (await FindShiftAsync(shiftId, httpContext, shiftStore, registers, shifts) is not { } found)
        {
            return ApiProblems.NotFound();
        }

        return await shifts.CloseAsync(found.Shift, counted, request.Note, httpContext.User, httpContext.RequestAborted)
            ? TypedResults.Ok(await ToApiAsync(found.Shift, shifts, httpContext))
            : ApiProblems.Conflict(PosCodes.ShiftClosed, "The shift is already closed.");
    }

    private static async Task<Results<Created<ApiPosSale>, ValidationProblem, ProblemHttpResult>> StartSaleAsync(
        ApiPosSaleRequest request,
        HttpContext httpContext,
        IRegisterStore registers,
        IRegisterShiftStore shiftStore,
        PosShiftService shifts,
        PosSaleService sales)
    {
        if (request?.Lines is not { Count: > 0 })
        {
            return ApiProblems.Validation("lines", "Ring up at least one product.");
        }

        if (await registers.FindByIdAsync(request.RegisterId, httpContext.RequestAborted) is not { } register || !await shifts.MaySellAtAsync(httpContext.User, register))
        {
            return ApiProblems.NotFound("There's no register with that id you may sell at.");
        }

        if (await shiftStore.FindOpenAsync(register.ItemId, httpContext.RequestAborted) is not { } shift)
        {
            return ApiProblems.Conflict(PosCodes.ShiftClosed, "Open a shift at the register first.");
        }

        var (buyer, customerFound) = await FindBuyerAsync(request, httpContext);

        if (!customerFound)
        {
            return ApiProblems.Validation("customerId", "There's no customer with that id.");
        }

        var result = await sales.StartAsync(register, shift, request.Lines, buyer, httpContext.User, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, string.Join(" ", result.Problems));
        }

        return TypedResults.Created(
            $"{httpContext.Request.PathBase}/{CommerceApi.RoutePrefix}/checkout/{result.Session.SessionId}",
            new ApiPosSale(result.Order.ItemId, result.Order.OrderNumber, CheckoutApi.Read(result.Session)));
    }

    private static async Task<Results<Ok<ApiPosPayment>, ValidationProblem, ProblemHttpResult>> PayAsync(
        string checkoutId,
        ApiPosPayRequest request,
        HttpContext httpContext,
        ICheckoutSessionStore sessionStore,
        IOrderStore orders,
        IRegisterStore registers,
        IRegisterShiftStore shiftStore,
        PosShiftService shifts,
        PosSaleService sales)
    {
        if (request?.Cash is < 0m)
        {
            return ApiProblems.Validation("cash", "Give the cash handed over.");
        }

        if (await CheckoutApi.FindOpenAsync(sessionStore, checkoutId, httpContext.RequestAborted) is not { } session ||
            !session.TryGet<Checkout.Models.CheckoutCounterSale>(out var counterSale) ||
            await registers.FindByIdAsync(counterSale.RegisterId, httpContext.RequestAborted) is not { } register ||
            !await shifts.MaySellAtAsync(httpContext.User, register))
        {
            return ApiProblems.NotFound();
        }

        // The money goes into the shift's drawer, so a sale whose shift has closed is rung up again on the open one.
        if (await shiftStore.FindByIdAsync(counterSale.ShiftId, httpContext.RequestAborted) is not { Status: RegisterShiftStatus.Open })
        {
            return ApiProblems.Conflict(PosCodes.ShiftClosed, "The sale's shift is closed; ring it up again on the open shift.");
        }

        var result = await sales.PayAsync(session, request?.Cash, request?.Card is not null, request?.Card?.ApprovalCode, request?.Reader?.ReaderId, httpContext.RequestAborted);
        var currency = session.TryGet<CheckoutInvoice>(out var invoice) ? invoice.Currency : session.Currency;

        if (result.Status == PosPaymentStatus.Failed)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, result.Error);
        }

        var order = result.Status == PosPaymentStatus.Completed ? await orders.FindByIdAsync(session.ReferenceId, httpContext.RequestAborted) : null;

        return TypedResults.Ok(new ApiPosPayment(
            result.Status switch
            {
                PosPaymentStatus.Completed => "completed",
                PosPaymentStatus.Pending => "pending",
                _ => "due",
            },
            ApiMoney.From(result.AmountDue, currency),
            ApiMoney.From(result.Change, currency),
            order is null ? null : PosReceipts.ToApi(order, result.Change)));
    }

    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> GetReceiptAsync(
        string orderId,
        HttpContext httpContext,
        IOrderStore orders,
        IRegisterStore registers,
        PosShiftService shifts)
    {
        if (await FindSaleAsync(orderId, httpContext, orders, registers, shifts) is not { } order)
        {
            return ApiProblems.NotFound();
        }

        if (await PosReceipts.RenderAsync(order, httpContext.RequestServices) is not { } html)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "Turn on Receipts to print receipts.");
        }

        return TypedResults.Content(html, "text/html; charset=utf-8");
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> EmailReceiptAsync(
        string orderId,
        ApiEmailReceiptRequest request,
        HttpContext httpContext,
        IOrderStore orders,
        IRegisterStore registers,
        PosShiftService shifts,
        IStringLocalizerFactory localizerFactory,
        ICommerceEmailSender emailSender)
    {
        var email = request?.Email?.Trim();

        if (string.IsNullOrEmpty(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return ApiProblems.Validation("email", "Give the email address to send it to.");
        }

        if (await FindSaleAsync(orderId, httpContext, orders, registers, shifts) is not { } order)
        {
            return ApiProblems.NotFound();
        }

        if (await PosReceipts.RenderAsync(order, httpContext.RequestServices) is not { } html || !emailSender.CanSend)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "Turn on Receipts and Email to email receipts.");
        }

        var sent = await emailSender.SendAsync(new CommerceEmail
        {
            To = email,
            Order = order,
            Reference = order.OrderNumber,
        }, _ => Task.FromResult(new CommerceEmailContent
        {
            Subject = localizerFactory.Create(typeof(PosApiEndpoints))["Receipt for order {0}", order.OrderNumber],
            Html = html,
        }), httpContext.RequestAborted);

        return sent
            ? TypedResults.NoContent()
            : ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, "The receipt couldn't be sent.");
    }

    private static async Task<Results<Ok<ApiPosReturn>, ValidationProblem, ProblemHttpResult>> ReturnAsync(
        ApiPosReturnRequest request,
        HttpContext httpContext,
        IOrderStore orders,
        IRegisterStore registers,
        IRegisterShiftStore shiftStore,
        PosShiftService shifts,
        OrderOperationsService operations,
        IAuthorizationService authorizationService,
        ICheckoutTenderLedger ledger)
    {
        if (request?.Amount is <= 0m)
        {
            return ApiProblems.Validation("amount", "Give the amount to refund, or none to refund all of it.");
        }

        if (await registers.FindByIdAsync(request?.RegisterId, httpContext.RequestAborted) is not { } register || !await shifts.MaySellAtAsync(httpContext.User, register))
        {
            return ApiProblems.NotFound("There's no register with that id you may sell at.");
        }

        // A till returns the store's register sales; an online order is returned there only by staff who manage orders.
        if (await orders.FindByIdAsync(request.OrderId, httpContext.RequestAborted) is not { Status: OrderStatus.Paid } order ||
            (!OrderChannels.IsCounterSale(order) && !await authorizationService.AuthorizeAsync(httpContext.User, OrderPermissions.ManageOrders)))
        {
            return ApiProblems.NotFound("There's no paid order with that id you may return.");
        }

        // Cash given back comes out of this register's drawer, so the drawer must be open to count it.
        if (await shiftStore.FindOpenAsync(register.ItemId, httpContext.RequestAborted) is not { } shift)
        {
            return ApiProblems.Conflict(PosCodes.ShiftClosed, "Open a shift at the register first.");
        }

        var cashBefore = await GetCashHeldAsync(ledger, order, httpContext.RequestAborted);
        var result = await operations.RefundAsync(order, request.Amount, request.Reason ?? "Returned at the till", request.Restock != false, PosShiftService.GetName(httpContext.User), asStoreCredit: false, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, string.Join(" ", result.Errors));
        }

        // What the refund put back on the sale's cash goes back in cash, out of this register's drawer. The card is
        // refunded first, so this is only what the card didn't cover.
        var cashBack = Math.Max(0m, cashBefore - await GetCashHeldAsync(ledger, order, httpContext.RequestAborted));

        if (cashBack > 0m)
        {
            await shifts.MoveCashAsync(shift, -cashBack, $"Refund of order {order.OrderNumber}", httpContext.User, httpContext.RequestAborted);
        }

        return TypedResults.Ok(new ApiPosReturn(order.ItemId, order.OrderNumber, ApiMoney.From(result.Refunded, order.Currency), ApiMoney.From(cashBack, order.Currency), result.Messages));
    }

    // The cash a sale was paid with that hasn't been given back yet.
    private static async Task<decimal> GetCashHeldAsync(ICheckoutTenderLedger ledger, Order order, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(order.CheckoutSessionId)
            ? 0m
            : (await ledger.GetRedeemedAsync(order.CheckoutSessionId, cancellationToken))
                .Where(hold => hold.ProviderKey == PosConstants.CashTenderKey)
                .Sum(hold => hold.Amount - hold.ReturnedAmount);

    // A shift opens for whoever may sell at its register.
    private static async Task<(RegisterShift Shift, Register Register)?> FindShiftAsync(string shiftId, HttpContext httpContext, IRegisterShiftStore shiftStore, IRegisterStore registers, PosShiftService shifts)
        => await shiftStore.FindByIdAsync(shiftId, httpContext.RequestAborted) is { } shift &&
            await registers.FindByIdAsync(shift.RegisterId, httpContext.RequestAborted) is { } register &&
            await shifts.MaySellAtAsync(httpContext.User, register)
                ? (shift, register)
                : null;

    // A register's sale opens for whoever may sell at that register.
    private static async Task<Order> FindSaleAsync(string orderId, HttpContext httpContext, IOrderStore orders, IRegisterStore registers, PosShiftService shifts)
        => await orders.FindByIdAsync(orderId, httpContext.RequestAborted) is { } order &&
            OrderChannels.IsCounterSale(order) &&
            await registers.FindByIdAsync(order.RegisterId, httpContext.RequestAborted) is { } register &&
            await shifts.MaySellAtAsync(httpContext.User, register)
                ? order
                : null;

    // The buyer the sale is for: a customer by id, someone by email, or nobody. A customer id that isn't found is the
    // till's mistake, so it isn't quietly sold to nobody.
    private static async Task<(PosBuyer Buyer, bool CustomerFound)> FindBuyerAsync(ApiPosSaleRequest request, HttpContext httpContext)
    {
        if (!string.IsNullOrEmpty(request.CustomerId))
        {
            if (httpContext.RequestServices.GetService<Customers.Core.Services.ICustomerStore>() is not { } customers ||
                await customers.FindByIdAsync(request.CustomerId, httpContext.RequestAborted) is not { } customer)
            {
                return (null, false);
            }

            return (new PosBuyer
            {
                CustomerId = customer.ItemId,
                UserId = customer.UserId,
                Name = customer.GetName(),
                Email = customer.Email,
                Phone = customer.Phone,
            }, true);
        }

        return (string.IsNullOrWhiteSpace(request.CustomerEmail)
            ? null
            : new PosBuyer { Email = request.CustomerEmail.Trim(), Name = request.CustomerName?.Trim() }, true);
    }

    private static async Task<ApiShift> ToApiAsync(RegisterShift shift, PosShiftService shifts, HttpContext httpContext)
    {
        var summary = shift.Summary ?? await shifts.SummarizeAsync(shift, httpContext.RequestAborted);
        var currency = shift.Currency;

        return new ApiShift(
            shift.ItemId,
            shift.RegisterId,
            shift.RegisterName,
            shift.Status == RegisterShiftStatus.Open ? "open" : "closed",
            ApiMoney.From(shift.OpeningFloat, currency),
            summary.Sales,
            ApiMoney.From(summary.Total, currency),
            ApiMoney.From(summary.Cash, currency),
            ApiMoney.From(summary.Card, currency),
            ApiMoney.From(summary.Other, currency),
            ApiMoney.From(summary.Refunded, currency),
            shift.CashMovements.Select(movement => new ApiCashMovement(ApiMoney.From(movement.Amount, currency), movement.Reason, movement.ByName, movement.Utc)).ToList(),
            ApiMoney.From(shift.ExpectedCash ?? shift.GetExpectedCash(summary.Cash), currency),
            ApiMoney.From(shift.CountedCash, currency),
            shift.CountedCash is { } counted ? ApiMoney.From(counted - (shift.ExpectedCash ?? 0m), currency) : null,
            shift.OpenedByName,
            shift.OpenedUtc,
            shift.ClosedByName,
            shift.ClosedUtc);
    }
}

/// <summary>
/// The conflict codes the point-of-sale endpoints add.
/// </summary>
public static class PosCodes
{
    public const string ShiftOpen = "shift-open";

    public const string ShiftClosed = "shift-closed";
}
