using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Orders;

/// <summary>
/// With Orders on, Invoicing owns the documents of a draft order the store sends its buyer (decision ID2): sending it
/// issues an Invoicing invoice from the order, or, for a quote with Invoicing Estimates on, sends an Invoicing estimate.
/// The order keeps one debt and one payment path, its own checkout: the invoice's transaction is the one amount owed, its
/// pay links lead to the order's pay page, and the order's payment is applied to it once the order is paid.
/// </summary>
[RequireFeatures(OrderConstants.Features.Orders)]
public sealed class OrderDocumentsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<OrderDocumentBridge>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrderDocumentSender, InvoicingOrderDocumentSender>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITransactionPayUrlProvider, OrderInvoicePayUrlProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrderEventHandler, OrderDocumentEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEstimatePayLinkProvider, OrderEstimatePayLinkProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEstimateObserver, OrderEstimateObserver>());
    }
}

/// <summary>
/// Makes, sends and settles the Invoicing documents of draft orders.
/// </summary>
public sealed class OrderDocumentBridge
{
    private readonly IInvoiceManager _invoices;
    private readonly IOrderStore _orders;
    private readonly ITransactionManager _transactions;
    private readonly OrderInvoiceBuilder _builder;
    private readonly IDocumentCalculator _calculator;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public OrderDocumentBridge(
        IInvoiceManager invoices,
        IOrderStore orders,
        ITransactionManager transactions,
        OrderInvoiceBuilder builder,
        IDocumentCalculator calculator,
        ISession session,
        IClock clock,
        IServiceProvider serviceProvider,
        ILogger<OrderDocumentBridge> logger,
        IStringLocalizer<OrderDocumentBridge> stringLocalizer)
    {
        _invoices = invoices;
        _orders = orders;
        _transactions = transactions;
        _builder = builder;
        _calculator = calculator;
        _session = session;
        _clock = clock;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets a value indicating whether Invoicing Estimates is on, so an order's quote is sent as an estimate.
    /// </summary>
    public bool EstimatesEnabled
        => _serviceProvider.GetService<IEstimateCommandService>() is not null;

    /// <summary>
    /// Finds the invoice made from the order, the latest one that isn't void.
    /// </summary>
    public async Task<Invoice> FindInvoiceAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(orderId))
        {
            return null;
        }

        var made = await _invoices.PageAsync(1, 20, new InvoiceQuery { SourceType = InvoicingConstants.OrderReferenceType, SourceId = orderId }, cancellationToken);

        return made.Entries
            .Where(invoice => invoice.Status != InvoiceStatus.Void && invoice.Kind != InvoiceKind.DebitNote)
            .OrderByDescending(invoice => invoice.CreatedUtc)
            .FirstOrDefault();
    }

    /// <summary>
    /// Finds the estimate made from the order's quote, the latest revision.
    /// </summary>
    public async Task<Estimate> FindEstimateAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(order?.OrderNumber))
        {
            return null;
        }

        var number = order.OrderNumber;
        var candidates = await _session
            .Query<Estimate, EstimateIndex>(index => index.Reference == number, collection: InvoicingConstants.CollectionName)
            .ListAsync(cancellationToken);

        return candidates
            .Where(estimate => string.Equals(estimate.SourceType, InvoicingConstants.OrderReferenceType, StringComparison.Ordinal) &&
                string.Equals(estimate.SourceId, order.ItemId, StringComparison.Ordinal) &&
                string.IsNullOrEmpty(estimate.ReplacedById))
            .OrderByDescending(estimate => estimate.CreatedUtc)
            .FirstOrDefault();
    }

    /// <summary>
    /// Sends a draft order as an invoice: issues one from the order the first time, then sends it (again, when it was
    /// sent before). Its debt is the order's one amount owed.
    /// </summary>
    public async Task<OrderDocumentSendResult> SendInvoiceAsync(Order order, CancellationToken cancellationToken = default)
    {
        var result = new OrderDocumentSendResult();
        var invoice = await FindInvoiceAsync(order.ItemId, cancellationToken);

        if (invoice is null || invoice.Status == InvoiceStatus.Draft)
        {
            invoice = await IssueFromOrderAsync(order, invoice, result, cancellationToken);

            if (!result.Sent)
            {
                return result;
            }
        }

        if (invoice.Status != InvoiceStatus.Open)
        {
            return result.Fail(S["Invoice {0} of this order is {1}, so it can't be sent.", invoice.Number, invoice.Status]);
        }

        if (_serviceProvider.GetService<ICommerceEmailSender>() is not { CanSend: true })
        {
            result.Note = S["Sent as invoice {0}. E-mail isn't set up, so share the invoice's link with the buyer.", invoice.Number];
            result.Message = result.Note;

            return result;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();

        var sent = await scope.ServiceProvider.GetRequiredService<IInvoiceSender>().SendAsync(new InvoiceSendRequest { InvoiceId = invoice.ItemId }, cancellationToken);

        if (!sent.Succeeded)
        {
            foreach (var error in sent.Errors)
            {
                result.Fail(S["Invoice {0} was issued but not sent: {1}", invoice.Number, error]);
            }

            return result;
        }

        result.Note = S["Sent as invoice {0} to {1}.", invoice.Number, order.CustomerEmail];
        result.Message = result.Note;

        return result;
    }

    /// <summary>
    /// Sends a draft order's quote as an estimate: made from the order the first time, with the quote's expiry and no
    /// invoice of its own on acceptance (the buyer pays the order), then sent.
    /// </summary>
    public async Task<OrderDocumentSendResult> SendEstimateAsync(Order order, CancellationToken cancellationToken = default)
    {
        var result = new OrderDocumentSendResult();
        var estimate = await FindEstimateAsync(order, cancellationToken);

        if (estimate is null || estimate.Status is EstimateStatus.Declined or EstimateStatus.Canceled or EstimateStatus.Expired)
        {
            estimate = await BuildEstimateAsync(order, cancellationToken);

            var manager = _serviceProvider.GetRequiredService<IEstimateManager>();
            var validation = await manager.ValidateAsync(estimate, cancellationToken);

            if (!validation.Succeeded)
            {
                foreach (var error in validation.Errors)
                {
                    result.Fail(error.ErrorMessage);
                }

                return result;
            }

            await manager.CreateAsync(estimate, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        if (estimate.Status is not (EstimateStatus.Draft or EstimateStatus.Sent))
        {
            return result.Fail(S["Estimate {0} of this quote is {1}, so it can't be sent.", estimate.Number, estimate.Status]);
        }

        await using var scope = _serviceProvider.CreateAsyncScope();

        var email = scope.ServiceProvider.GetService<ICommerceEmailSender>();

        if (email is not { CanSend: true })
        {
            if (estimate.Status == EstimateStatus.Draft)
            {
                var marked = await scope.ServiceProvider.GetRequiredService<IEstimateCommandService>().SendAsync(estimate.ItemId, cancellationToken);

                if (!marked.Succeeded)
                {
                    foreach (var error in marked.Errors)
                    {
                        result.Fail(error);
                    }

                    return result;
                }

                estimate = marked.Estimate;
            }

            result.Note = S["Sent as estimate {0}. E-mail isn't set up, so share the estimate's link with the buyer.", estimate.Number];
            result.Message = result.Note;

            return result;
        }

        var sent = await scope.ServiceProvider.GetRequiredService<EstimateSender>().SendAsync(estimate.ItemId, null, cancellationToken);

        if (!sent.Succeeded)
        {
            foreach (var error in sent.Errors)
            {
                result.Fail(error);
            }

            return result;
        }

        result.Note = S["Sent as estimate {0} to {1}.", sent.Estimate?.Number ?? estimate.Number, order.CustomerEmail];
        result.Message = result.Note;

        return result;
    }

    /// <summary>
    /// Settles the order's invoice from the order's own checkout once the order is paid. For a quote accepted from its
    /// estimate, the invoice is made then, and the estimate is marked invoiced.
    /// </summary>
    public async Task SettleAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (await _orders.FindByIdAsync(orderId, cancellationToken) is not { } order)
        {
            return;
        }

        var invoice = await FindInvoiceAsync(order.ItemId, cancellationToken);
        Estimate estimate = null;

        if (invoice is null)
        {
            // Only an order sent as an estimate gets its invoice now; any other paid order has no Invoicing document.
            estimate = EstimatesEnabled ? await FindEstimateAsync(order, cancellationToken) : null;

            if (estimate is not { Status: EstimateStatus.Accepted or EstimateStatus.Sent })
            {
                return;
            }

            var issued = new OrderDocumentSendResult();

            invoice = await IssueFromOrderAsync(order, null, issued, cancellationToken);

            if (!issued.Sent)
            {
                _logger.LogWarning("The invoice of paid order '{OrderNumber}' could not be issued: {Errors}", order.OrderNumber, string.Join(" ", issued.Errors));

                return;
            }

            await MarkEstimateInvoicedAsync(estimate.ItemId, invoice, cancellationToken);
        }

        if (invoice.Status != InvoiceStatus.Open ||
            string.IsNullOrEmpty(invoice.TransactionId) ||
            string.IsNullOrEmpty(order.CheckoutSessionId) ||
            await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken) is not { } transaction ||
            !IsPostedByInvoice(transaction) ||
            transaction.OutstandingAmount <= 0m ||
            _serviceProvider.GetService<IPaymentAttemptStore>() is not { } attempts ||
            _serviceProvider.GetService<ITransactionSettlementService>() is not { } settlement)
        {
            return;
        }

        var paid = (await attempts.GetBySessionAsync(order.CheckoutSessionId, cancellationToken))
            .Where(attempt => attempt.State == PaymentAttemptState.Succeeded)
            .ToArray();

        if (paid.Length == 0)
        {
            return;
        }

        // The order's checkout is this debt's payment: its refunds find the debt by the checkout and obligation.
        if (string.IsNullOrEmpty(transaction.CheckoutSessionId))
        {
            transaction.CheckoutSessionId = order.CheckoutSessionId;
            transaction.ObligationId ??= paid[0].ObligationId;

            await _transactions.UpdateAsync(transaction, data: null, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
            transaction = await _transactions.FindByIdAsync(transaction.ItemId, cancellationToken);
        }

        // The checkout adds shipping and tax the store's draft didn't have; what the buyer paid above the invoice is put
        // on a debit note of it, and the one payment settles both.
        var confirmed = paid.Sum(attempt => attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount);
        var owed = transaction.OutstandingAmount;

        if (paid.Length == 1 &&
            confirmed - owed > 0.01m &&
            await IssueCheckoutDifferenceAsync(order, invoice, confirmed - owed, cancellationToken) is { TransactionId: { Length: > 0 } debitTransactionId } &&
            await _transactions.FindByIdAsync(debitTransactionId, cancellationToken) is { } debitTransaction)
        {
            await settlement.ApplyShareAsync(debitTransaction, paid[0], confirmed - owed, order.CheckoutSessionId, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            transaction = await _transactions.FindByIdAsync(transaction.ItemId, cancellationToken);
            await settlement.ApplyShareAsync(transaction, paid[0], owed, order.CheckoutSessionId, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return;
        }

        await settlement.ApplyAsync(transaction, paid, order.CheckoutSessionId, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    // A debit note of the order's invoice for what its checkout added (shipping, tax), already included in the amount.
    private async Task<Invoice> IssueCheckoutDifferenceAsync(Order order, Invoice invoice, decimal amount, CancellationToken cancellationToken)
    {
        var debitNote = await _invoices.NewAsync(cancellationToken: cancellationToken);

        debitNote.Kind = InvoiceKind.DebitNote;
        debitNote.RelatedInvoiceId = invoice.ItemId;
        debitNote.SourceType = InvoicingConstants.OrderReferenceType;
        debitNote.SourceId = order.ItemId;
        debitNote.BillTo = invoice.BillTo;
        debitNote.Currency = invoice.Currency;
        debitNote.PricesIncludeTax = false;
        debitNote.DueDate = invoice.DueDate;
        debitNote.Reference = invoice.Number;
        debitNote.PurchaseOrderNumber = invoice.PurchaseOrderNumber;
        debitNote.TemplateId = invoice.TemplateId;
        debitNote.Notes = S["Added at checkout when order {0} was paid.", order.OrderNumber].Value;
        debitNote.Lines.Add(new DocumentLine
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentLineKind.Fee,
            Name = order.Shipping is { Amount: > 0m } shipping
                ? S["Shipping ({0}) and tax added at checkout", shipping.Name].Value
                : S["Added at checkout (shipping, tax)"].Value,
            Quantity = 1m,
            UnitPrice = amount,
            TaxTreatment = TaxTreatment.OutOfScope,
            TaxTreatmentReason = S["Paid at checkout, tax included"].Value,
        });

        var settings = await _serviceProvider.GetRequiredService<ISiteService>().GetSettingsAsync<InvoicingSettings>();

        await InvoiceCalculation.ApplyAsync(_calculator, debitNote, settings, issueDate: null, cancellationToken);

        if (!(await _invoices.ValidateAsync(debitNote, cancellationToken)).Succeeded)
        {
            return null;
        }

        await _invoices.CreateAsync(debitNote, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        await using var scope = _serviceProvider.CreateAsyncScope();

        var issued = await scope.ServiceProvider.GetRequiredService<IInvoiceCommandService>().IssueAsync(debitNote.ItemId, cancellationToken);

        if (!issued.Succeeded)
        {
            _logger.LogWarning("The checkout difference of order '{OrderNumber}' was not put on a debit note: {Errors}", order.OrderNumber, string.Join(" ", issued.Errors));

            return null;
        }

        return issued.Invoice;
    }

    /// <summary>
    /// Voids the order's unpaid invoice, and declines or cancels its estimate, when the order is canceled.
    /// </summary>
    public async Task CloseAsync(string orderId, bool declined, CancellationToken cancellationToken = default)
    {
        if (await _orders.FindByIdAsync(orderId, cancellationToken) is not { } order)
        {
            return;
        }

        if (await FindInvoiceAsync(order.ItemId, cancellationToken) is { Status: InvoiceStatus.Open, AmountPaid: 0m } invoice)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            var voided = await scope.ServiceProvider.GetRequiredService<IInvoiceCommandService>().VoidAsync(invoice.ItemId, S["Order {0} was canceled.", order.OrderNumber], cancellationToken);

            if (!voided.Succeeded)
            {
                _logger.LogWarning("The invoice of canceled order '{OrderNumber}' was not voided: {Errors}", order.OrderNumber, string.Join(" ", voided.Errors));
            }
        }

        if (EstimatesEnabled && await FindEstimateAsync(order, cancellationToken) is { Status: EstimateStatus.Draft or EstimateStatus.Sent } estimate)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            var commands = scope.ServiceProvider.GetRequiredService<IEstimateCommandService>();

            _ = declined && estimate.Status == EstimateStatus.Sent
                ? await commands.DeclineAsync(estimate.ItemId, S["The buyer turned the quote down."], cancellationToken)
                : await commands.CancelAsync(estimate.ItemId, S["Order {0} was canceled.", order.OrderNumber], cancellationToken);
        }
    }

    /// <summary>
    /// Records money the order gave back on its invoice with a credit note, once the invoice is issued. The refund itself
    /// was made through the order's checkout and is already on the invoice's transaction, so the credit note moves nothing.
    /// </summary>
    public async Task CreditRefundAsync(string orderId, decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        if (amount <= 0m ||
            await _orders.FindByIdAsync(orderId, cancellationToken) is not { } order ||
            await FindInvoiceAsync(order.ItemId, cancellationToken) is not { Status: InvoiceStatus.Open } invoice)
        {
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();

        var commands = scope.ServiceProvider.GetRequiredService<ICreditNoteCommandService>();
        var draft = await commands.CreateDraftAsync(invoice.ItemId, cancellationToken);

        if (!draft.Succeeded || draft.CreditNote is not { } creditNote)
        {
            _logger.LogWarning("No credit note was drafted for the refund of order '{OrderNumber}': {Errors}", order.OrderNumber, string.Join(" ", draft.Errors));

            return;
        }

        creditNote.Disposition = CreditNoteDisposition.RefundedElsewhere;
        creditNote.Reason = string.IsNullOrWhiteSpace(reason)
            ? S["Refund of order {0}", order.OrderNumber].Value
            : S["Refund of order {0}: {1}", order.OrderNumber, reason].Value;

        // A part refund is credited as one amount, outside tax: the order's refund was an amount, not lines.
        if (Math.Abs((invoice.Totals?.Total ?? 0m) - amount) > 0.01m)
        {
            creditNote.Lines.Clear();
            creditNote.Lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = DocumentLineKind.Fee,
                Name = S["Refund of order {0}", order.OrderNumber].Value,
                Quantity = 1m,
                UnitPrice = amount,
                TaxTreatment = TaxTreatment.OutOfScope,
                TaxTreatmentReason = S["Refund"].Value,
            });
        }

        var store = scope.ServiceProvider.GetRequiredService<ICreditNoteStore>();

        await store.UpdateAsync(creditNote, cancellationToken);
        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        var issued = await commands.IssueAsync(creditNote.ItemId, cancellationToken);

        if (!issued.Succeeded)
        {
            _logger.LogWarning("The credit note for the refund of order '{OrderNumber}' was left as a draft: {Errors}", order.OrderNumber, string.Join(" ", issued.Errors));
        }
    }

    /// <summary>
    /// Returns the address of the order's pay page, when the order can still be paid there.
    /// </summary>
    public async Task<string> GetOrderPayUrlAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (await _orders.FindByIdAsync(orderId, cancellationToken) is not { } order ||
            order.Invoice is null ||
            order.Status is not (OrderStatus.Draft or OrderStatus.AwaitingPayment) ||
            order.Quote is { } quote && quote.Status != OrderQuoteStatus.Accepted && !quote.CanAccept(_clock.UtcNow) ||
            order.Approval?.Status is OrderApprovalStatus.Pending or OrderApprovalStatus.Rejected)
        {
            return null;
        }

        return await _serviceProvider.GetRequiredService<IOrderLinkService>().CreatePayUrlAsync(order);
    }

    // A debt the invoice posted itself when issued, not one it took over from the order's checkout (paid on account).
    internal static bool IsPostedByInvoice(Transaction transaction)
        => string.Equals(transaction?.ReferenceType, InvoicingConstants.InvoiceReferenceType, StringComparison.Ordinal);

    // Issues the order's invoice: made from the order (or the draft left before), checked against the order's total.
    private async Task<Invoice> IssueFromOrderAsync(Order order, Invoice draft, OrderDocumentSendResult result, CancellationToken cancellationToken)
    {
        if (draft is null)
        {
            draft = await _builder.BuildDraftAsync(order, transaction: null, cancellationToken);
            draft.Notes = order.Invoice?.Note;

            var validation = await _invoices.ValidateAsync(draft, cancellationToken);

            if (!validation.Succeeded)
            {
                foreach (var error in validation.Errors)
                {
                    result.Fail(error.ErrorMessage);
                }

                return null;
            }

            await _invoices.CreateAsync(draft, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        // The buyer pays the order's total at its checkout; an invoice that comes to something else would leave a
        // difference owed, so it waits for staff to correct it.
        if (order.Total is { } total && Math.Abs((draft.Totals?.Total ?? 0m) - total) > 0.01m)
        {
            return Fail(result, draft, S["Draft invoice of order {0} comes to {1}, but the order to {2}. Correct the draft invoice, then send the order again.", order.OrderNumber, draft.Totals?.Total ?? 0m, total]);
        }

        await using var scope = _serviceProvider.CreateAsyncScope();

        var issued = await scope.ServiceProvider.GetRequiredService<IInvoiceCommandService>().IssueAsync(draft.ItemId, cancellationToken);

        if (!issued.Succeeded)
        {
            foreach (var error in issued.Errors)
            {
                result.Fail(error);
            }

            return draft;
        }

        if (issued.NeedsApproval)
        {
            return Fail(result, issued.Invoice, S["The invoice of order {0} is waiting for approval; send the order again once it is approved.", order.OrderNumber]);
        }

        return issued.Invoice;
    }

    private static Invoice Fail(OrderDocumentSendResult result, Invoice invoice, string error)
    {
        result.Fail(error);

        return invoice;
    }

    private async Task<Estimate> BuildEstimateAsync(Order order, CancellationToken cancellationToken)
    {
        var invoice = await _builder.BuildDraftAsync(order, transaction: null, cancellationToken);
        var estimate = await _serviceProvider.GetRequiredService<IEstimateManager>().NewAsync(cancellationToken: cancellationToken);

        estimate.SourceType = invoice.SourceType;
        estimate.SourceId = invoice.SourceId;
        estimate.Reference = invoice.Reference;
        estimate.PurchaseOrderNumber = invoice.PurchaseOrderNumber;
        estimate.Currency = invoice.Currency;
        estimate.Culture = invoice.Culture;
        estimate.PricesIncludeTax = invoice.PricesIncludeTax;
        estimate.BillTo = invoice.BillTo;
        estimate.Notes = order.Invoice?.Note;
        estimate.ExpiryDate = invoice.DueDate;

        // Accepting it accepts the quote; the buyer pays the order, which is invoiced then.
        estimate.OnAcceptance = EstimateConversion.None;

        foreach (var line in invoice.Lines)
        {
            estimate.Lines.Add(line);
        }

        var settings = await _serviceProvider.GetRequiredService<ISiteService>().GetSettingsAsync<InvoicingSettings>();

        await InvoiceCalculation.ApplyAsync(_calculator, estimate, settings, issueDate: null, cancellationToken);

        return estimate;
    }

    private async Task MarkEstimateInvoicedAsync(string estimateId, Invoice invoice, CancellationToken cancellationToken)
    {
        var manager = _serviceProvider.GetRequiredService<IEstimateManager>();

        if (await manager.FindByIdAsync(estimateId, cancellationToken) is not { } estimate)
        {
            return;
        }

        if (!estimate.InvoiceIds.Contains(invoice.ItemId))
        {
            estimate.InvoiceIds.Add(invoice.ItemId);
        }

        estimate.Status = EstimateStatus.Invoiced;
        estimate.ClosedUtc ??= _clock.UtcNow;
        estimate.History.Add(new DocumentEvent
        {
            Id = IdGenerator.GenerateId(),
            CreatedUtc = _clock.UtcNow,
            Kind = "Invoiced",
            Message = S["The order was paid and invoiced as {0}.", invoice.Number],
        });

        await manager.UpdateAsync(estimate, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Sends a draft order as an Invoicing invoice, or its quote as an Invoicing estimate when Invoicing Estimates is on.
/// </summary>
internal sealed class InvoicingOrderDocumentSender : IOrderDocumentSender
{
    private readonly OrderDocumentBridge _bridge;
    private readonly IServiceProvider _serviceProvider;

    public InvoicingOrderDocumentSender(OrderDocumentBridge bridge, IServiceProvider serviceProvider = null)
    {
        _bridge = bridge;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> GetInvoiceUrlAsync(Order order, bool forStaff, CancellationToken cancellationToken = default)
    {
        if (await _bridge.FindInvoiceAsync(order?.ItemId, cancellationToken) is not { Status: InvoiceStatus.Open } invoice)
        {
            return null;
        }

        if (forStaff)
        {
            return _serviceProvider?.GetService<LinkGenerator>() is { } links && _serviceProvider.GetService<IHttpContextAccessor>()?.HttpContext is { } httpContext
                ? links.GetPathByName(httpContext, "InvoicesDetail", new { itemId = invoice.ItemId })
                : null;
        }

        return _serviceProvider?.GetService<IInvoiceShareLinkService>() is { } shareLinks
            ? await shareLinks.CreateUrlAsync(invoice, cancellationToken)
            : null;
    }

    public async Task<OrderDocumentSendResult> SendAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Quote is not null)
        {
            // Without Invoicing Estimates, the order's quote keeps its own e-mail.
            return _bridge.EstimatesEnabled ? await _bridge.SendEstimateAsync(order, cancellationToken) : null;
        }

        return await _bridge.SendInvoiceAsync(order, cancellationToken);
    }
}

/// <summary>
/// Sends the payer of a draft order's invoice to the order's pay page, its one payment path.
/// </summary>
internal sealed class OrderInvoicePayUrlProvider : ITransactionPayUrlProvider
{
    private readonly IInvoiceStore _invoices;
    private readonly OrderDocumentBridge _bridge;

    public OrderInvoicePayUrlProvider(IInvoiceStore invoices, OrderDocumentBridge bridge)
    {
        _invoices = invoices;
        _bridge = bridge;
    }

    public async Task<string> GetPayUrlAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        if (!OrderDocumentBridge.IsPostedByInvoice(transaction) ||
            await InvoiceTransactionLink.FindInvoiceAsync(_invoices, transaction, cancellationToken) is not { } invoice ||
            !string.Equals(invoice.SourceType, InvoicingConstants.OrderReferenceType, StringComparison.Ordinal))
        {
            return null;
        }

        return await _bridge.GetOrderPayUrlAsync(invoice.SourceId, cancellationToken);
    }
}

/// <summary>
/// Sends a customer who accepted an order's estimate to the order's pay page.
/// </summary>
internal sealed class OrderEstimatePayLinkProvider : IEstimatePayLinkProvider
{
    private readonly OrderDocumentBridge _bridge;

    public OrderEstimatePayLinkProvider(OrderDocumentBridge bridge)
    {
        _bridge = bridge;
    }

    public Task<string> GetPayUrlAsync(Estimate estimate, CancellationToken cancellationToken = default)
        => string.Equals(estimate?.SourceType, InvoicingConstants.OrderReferenceType, StringComparison.Ordinal)
            ? _bridge.GetOrderPayUrlAsync(estimate.SourceId, cancellationToken)
            : Task.FromResult<string>(null);
}

/// <summary>
/// Follows what happens to an order with Invoicing documents: paid (its invoice settled from the order's checkout, made
/// first for an accepted estimate), canceled (its invoice voided, its estimate declined or canceled) and refunded (a
/// credit note). Each runs once the request has saved the order, as its own unit of work.
/// </summary>
internal sealed class OrderDocumentEventHandler : OrderEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    public OrderDocumentEventHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        var orderId = context.Order.ItemId;

        return DeferAsync(bridge => bridge.SettleAsync(orderId, CancellationToken.None), cancellationToken);
    }

    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
    {
        var orderId = context.Order.ItemId;
        var declined = context.Reason == OrderCancelReason.Declined;

        return DeferAsync(bridge => bridge.CloseAsync(orderId, declined, CancellationToken.None), cancellationToken);
    }

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
    {
        var orderId = context.Order.ItemId;
        var amount = context.Refund?.Amount ?? 0m;
        var reason = context.Refund?.Reason;

        return DeferAsync(bridge => bridge.CreditRefundAsync(orderId, amount, reason, CancellationToken.None), cancellationToken);
    }

    private Task DeferAsync(Func<OrderDocumentBridge, Task> work, CancellationToken cancellationToken)
    {
        if (ShellScope.Current is null)
        {
            return work(_serviceProvider.GetRequiredService<OrderDocumentBridge>());
        }

        ShellScope.AddDeferredTask(scope => work(scope.ServiceProvider.GetRequiredService<OrderDocumentBridge>()));

        return Task.CompletedTask;
    }
}

/// <summary>
/// Carries an order estimate's answer to the order's quote: accepted lets the buyer pay the order; declined turns the
/// quote down and cancels the order, as declining it on the order does.
/// </summary>
internal sealed class OrderEstimateObserver : IEstimateObserver
{
    private readonly IOrderStore _orders;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public OrderEstimateObserver(
        IOrderStore orders,
        IServiceProvider serviceProvider,
        ISession session,
        IClock clock,
        IStringLocalizer<OrderEstimateObserver> stringLocalizer)
    {
        _orders = orders;
        _serviceProvider = serviceProvider;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task AcceptedAsync(Estimate estimate, CancellationToken cancellationToken = default)
    {
        if (await FindQuoteOrderAsync(estimate, cancellationToken) is not { Quote: { Status: OrderQuoteStatus.Sent } quote } order)
        {
            return;
        }

        quote.Status = OrderQuoteStatus.Accepted;
        quote.RespondedUtc = _clock.UtcNow;

        var orderService = _serviceProvider.GetRequiredService<IOrderService>();

        await orderService.AddNoteAsync(order, S["The buyer accepted estimate {0}.", estimate.Number], OrderNoteKind.Event, estimate.Acceptance?.Name, cancellationToken);
        await _orders.UpdateAsync(order, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    public async Task DeclinedAsync(Estimate estimate, CancellationToken cancellationToken = default)
    {
        if (await FindQuoteOrderAsync(estimate, cancellationToken) is not { Quote: { Status: OrderQuoteStatus.Requested or OrderQuoteStatus.Sent } quote } order ||
            order.Status != OrderStatus.Draft)
        {
            return;
        }

        quote.Status = OrderQuoteStatus.Declined;
        quote.RespondedUtc = _clock.UtcNow;

        var orderService = _serviceProvider.GetRequiredService<IOrderService>();

        await orderService.AddNoteAsync(order, S["The buyer turned down estimate {0}.", estimate.Number], OrderNoteKind.Event, null, cancellationToken);
        await orderService.CancelAsync(order, OrderCancelReason.Declined, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    private async Task<Order> FindQuoteOrderAsync(Estimate estimate, CancellationToken cancellationToken)
        => string.Equals(estimate?.SourceType, InvoicingConstants.OrderReferenceType, StringComparison.Ordinal) && !string.IsNullOrEmpty(estimate.SourceId)
            ? await _orders.FindByIdAsync(estimate.SourceId, cancellationToken)
            : null;
}
