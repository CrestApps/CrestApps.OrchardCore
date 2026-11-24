using CrestApps.Core.AI;
using CrestApps.OrchardCore.Orders.AI;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders;

/// <summary>
/// Registers the order AI tools whenever the Orders and AI features are on. Every tool needs <c>ManageOrders</c>, as
/// Commerce › Orders. Refunding, canceling, and emailing a confirmation or an invoice ask for approval; a refund needs
/// its amount, currency, an idempotency key and the token of a <c>previewRefund</c> of the same refund, and a
/// cancellation never refunds.
/// </summary>
[Feature(OrderConstants.Features.Orders)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<OrdersToolScope>();

        // previewRefund plans a refund with the order operations service itself, so the plan follows the refund.
        services.AddScoped<IOrderRefundPlanner>(serviceProvider => serviceProvider.GetRequiredService<OrderOperationsService>());

        services.AddCoreAITool<ListOrdersTool>(ListOrdersTool.TheName)
            .WithTitle(S["List Orders"])
            .WithDescription(S["Lists orders with number, date, customer, status, channel, fulfilment status, total, refunded amount and whether money is still owed. Filters by order number or customer email, status (placed, paid, awaiting payment, canceled, drafts, any), channel, customer, and placed-between dates, with paging; it also returns the link that exports the same list as CSV."])
            .WithCategory(S["Orders"])
            .Selectable();

        services.AddCoreAITool<GetOrderTool>(GetOrderTool.TheName)
            .WithTitle(S["Get Order"])
            .WithDescription(S["Returns one order by id or order number: customer and addresses, lines, discounts, tax, totals, how it was paid, notes, refunds, disputes, payment risk, what can still be refunded and what is still owed, plus links to its admin page, invoice and packing slip, and whether it has a pay link. Use it before refunding, canceling, shipping or answering a customer."])
            .WithCategory(S["Orders"])
            .Selectable();

        services.AddCoreAITool<PreviewRefundTool>(PreviewRefundTool.TheName)
            .WithTitle(S["Preview Refund"])
            .WithDescription(S["Shows what refundOrder would do, without moving money: what is still refundable for the order, the amount, and where it would go back to (newest online payment first, then Pay Later and offline payments, then gift cards and store credit, or store credit when asked), with any payment that would be skipped. Call it before every refund and show the result to the user. It returns a previewToken that refundOrder requires: it works once, only for you and for the same order, amount, currency and options, and expires after 10 minutes."])
            .WithCategory(S["Orders"])
            .Selectable();

        services.AddCoreAITool<RefundOrderTool>(RefundOrderTool.TheName)
            .WithTitle(S["Refund Order"])
            .WithDescription(S["Gives money back for an order: a stated amount and currency, or everything still refundable with refundAll, newest payment first through the payment provider, optionally putting the items back into stock or giving the amount as store credit instead. Call previewRefund first and show its result to the user: this tool needs the previewToken it returns, for the same order, amount, currency and options, and a new idempotency key; a used or expired token and a repeated key are refused. A refund cannot be undone; a payment method that cannot refund automatically is recorded for settling by hand. For a buyer's return, give its returnId: the refund then closes the return and emails the buyer."])
            .WithCategory(S["Orders"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<CancelOrderTool>(CancelOrderTool.TheName)
            .WithTitle(S["Cancel Order"])
            .WithDescription(S["Cancels an order with an optional note, optionally putting what a paid order sold back into stock; anything still owed for it is canceled too. It never refunds: what was paid stays refundable with refundOrder. A canceled order cannot be reopened."])
            .WithCategory(S["Orders"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ResendOrderConfirmationTool>(ResendOrderConfirmationTool.TheName)
            .WithTitle(S["Resend Order Confirmation"])
            .WithDescription(S["Emails a paid order's confirmation to the customer again and notes it on the order. It needs the order to have a customer email and the site's email to be set up."])
            .WithCategory(S["Orders"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<SaveDraftOrderTool>(SaveDraftOrderTool.TheName)
            .WithTitle(S["Save Draft Order"])
            .WithDescription(S["Creates or changes an order the store puts together for a buyer, or answers a quote request: buyer email and name, products with variant, quantity and an optional quoted price, a note and a pay-by date. It returns the priced lines and any stock problems, holds the items from stock when saved, can price without saving, and can start a new draft that adds items to a paid order."])
            .WithCategory(S["Orders"])
            .Selectable();

        services.AddCoreAITool<SendOrderInvoiceTool>(SendOrderInvoiceTool.TheName)
            .WithTitle(S["Send Order Invoice"])
            .WithDescription(S["Emails the buyer the invoice of a draft order, or the quote, with the link to review and pay it; sending again is allowed while it is unpaid. It needs the site's email and base URL to be set up."])
            .WithCategory(S["Orders"])
            .RequireApproval()
            .Selectable();
    }
}
