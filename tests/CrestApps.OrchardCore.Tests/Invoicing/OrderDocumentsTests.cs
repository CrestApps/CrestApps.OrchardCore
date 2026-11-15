using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Orders;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Taxation.Core.Models;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// With Invoicing on, a draft order the store sends is an Invoicing invoice (decision ID2): one invoice and one debt per
/// order, its pay link leads to the order's own pay page, and the order's checkout settles it. A quote is an Invoicing
/// estimate when Invoicing Estimates is on: accepting it lets the buyer pay the order, which then invoices it. Each
/// feature switched off leaves the order's own documents.
/// </summary>
public sealed class OrderDocumentsTests
{
    private const string PayUrlPrefix = "https://shop.example/orders/pay/";

    [Fact]
    public async Task ADraftOrderSentAsAnInvoice_HasOneInvoiceAndOneDebt_AndPayingThroughTheInvoicesLinkSettlesBoth()
    {
        // Arrange: a $100 draft order the store sends.
        await using var harness = await CreateHarnessAsync();
        harness.CanSendEmail = true;
        var order = NewDraftOrder();
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act: sent; the buyer follows the invoice's pay link, and pays the order at its own checkout.
        var sent = await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));
        var invoice = await OnBridgeAsync(harness, bridge => bridge.FindInvoiceAsync(order.ItemId, TestContext.Current.CancellationToken));
        var payUrl = await harness.InScopeAsync(async services => await services.GetRequiredService<ITransactionPayLinkService>().CreatePayUrlAsync(await harness.FindTransactionAsync(invoice.TransactionId), TestContext.Current.CancellationToken));

        await PayOrderAsync(harness, order, 100m);

        // Assert
        Assert.True(sent.Sent, string.Join(" ", sent.Errors));
        Assert.Equal((true, invoice.Number), (sent.Emailed, sent.DocumentNumber));
        Assert.Equal((InvoiceStatus.Open, InvoicingConstants.OrderReferenceType, "1042", 100m), (invoice.Status, invoice.SourceType, invoice.Reference, invoice.Totals.Total));
        Assert.Equal(PayUrlPrefix + order.ItemId, payUrl);
        Assert.Single(harness.Emails);

        var debts = await DebtsAsync(harness);
        var debt = Assert.Single(debts);
        Assert.Equal((invoice.TransactionId, TransactionStatus.Paid, 100m), (debt.ItemId, debt.Status, debt.AmountPaid));

        var settled = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal((InvoiceSettlement.Paid, 0m), (settled.Settlement, settled.AmountDue));
    }

    [Fact]
    public async Task ShippingAddedAtTheOrdersCheckout_IsPutOnADebitNote_AndTheOnePaymentSettlesBoth()
    {
        // Arrange: a $100 draft order sent as an invoice; at its checkout the buyer chose $12 shipping and paid $112.
        await using var harness = await CreateHarnessAsync();
        var order = NewDraftOrder();
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));
        order.Shipping = new OrderShipping { Name = "Ground", Amount = 12m };

        // Act
        await PayOrderAsync(harness, order, 112m);

        // Assert: the invoice is paid, and a $12 debit note of it is paid by the same payment.
        var invoice = await OnBridgeAsync(harness, bridge => bridge.FindInvoiceAsync(order.ItemId, TestContext.Current.CancellationToken));
        Assert.Equal((InvoiceSettlement.Paid, 100m), (invoice.Settlement, invoice.AmountPaid));

        var debts = await DebtsAsync(harness);
        Assert.Equal(2, debts.Count);
        Assert.All(debts, debt => Assert.Equal(TransactionStatus.Paid, debt.Status));
        Assert.Equal([12m, 100m], debts.Select(debt => debt.AmountPaid).Order());
    }

    [Fact]
    public async Task RefundingAnOrderWhoseCheckoutAddedShipping_CreditsTheInvoiceAndItsDebitNote_AndNothingIsOwedAgain()
    {
        // Arrange: a $100 invoice and its $12 debit note, both paid by the order checkout's one $112 payment.
        await using var harness = await CreateHarnessAsync();
        var order = NewDraftOrder();
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));
        order.Shipping = new OrderShipping { Name = "Ground", Amount = 12m };
        await PayOrderAsync(harness, order, 112m);

        // Act: the store refunds everything from the order's page. The ledger records the refund; the order's refunded
        // event runs after the request saves, as its handler defers it.
        await harness.InScopeAsync(async services =>
        {
            var ledger = new TransactionRefundHandler(services.GetRequiredService<ITransactionManager>(), services, services.GetRequiredService<IClock>(), NullLogger<TransactionRefundHandler>.Instance, new PassThroughStringLocalizer<TransactionRefundHandler>());

            await ledger.RefundSucceededAsync(new PaymentRefund
            {
                ItemId = "refund-1",
                SessionId = order.CheckoutSessionId,
                ObligationId = "obligation-" + order.ItemId,
                OriginalAttemptId = "attempt-" + order.ItemId,
                ProviderKey = "test",
                Currency = "USD",
                RefundGrossAmount = 112m,
                Status = RefundStatus.Succeeded,
            }, TestContext.Current.CancellationToken);

            return true;
        });

        await harness.InScopeAsync(async services =>
        {
            foreach (var handler in services.GetServices<IOrderEventHandler>())
            {
                await handler.RefundedAsync(new OrderRefundedContext(order, new OrderRefund { Amount = 112m }), TestContext.Current.CancellationToken);
            }

            return true;
        });

        // Assert: each debt took back its own share of the payment, and each document is settled by a credit note, so
        // neither is owed again.
        var debts = await DebtsAsync(harness);
        Assert.Equal([(12m, 12m), (100m, 100m)], debts.Select(debt => (debt.AmountPaid, debt.AmountRefunded)).Order());

        var documents = await harness.InScopeAsync(services => services.GetRequiredService<IInvoiceManager>().PageAsync(1, 10, new InvoiceQuery { SourceType = InvoicingConstants.OrderReferenceType, SourceId = order.ItemId }, TestContext.Current.CancellationToken));
        Assert.Equal(2, documents.Entries.Count);
        Assert.All(documents.Entries, document => Assert.Equal((InvoiceSettlement.Paid, document.Totals.Total, 0m), (document.Settlement, document.AmountCredited, document.AmountDue)));
    }

    [Fact]
    public async Task ACouponTakenAtTheOrdersCheckout_IsCreditedWithItsTaxByRate_AndTheInvoiceSettles()
    {
        // Arrange: a widget ($100 at 10%) and a setup service ($50 at 20%), invoiced at $170. At the order's checkout a
        // 10% coupon took $15 off before tax, so the buyer paid $153: $90 + $9 tax and $45 + $9 tax.
        await using var harness = await CreateHarnessAsync(taxed: true);
        var order = NewTaxedDraftOrder();
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));
        order.Discounts = [new OrderDiscount { Code = "TEN", Amount = 15m }];
        order.Total = 153m;

        // Act
        await PayOrderAsync(harness, order, 153m);

        // Assert: a $17 credit note lowered the invoice to what was paid, with its tax split by rate as the discount's
        // was: $10 at 10% ($1 tax) and $5 at 20% ($1 tax).
        var invoice = await OnBridgeAsync(harness, bridge => bridge.FindInvoiceAsync(order.ItemId, TestContext.Current.CancellationToken));
        Assert.Equal((170m, InvoiceSettlement.Paid, 153m, 17m, 0m), (invoice.Totals.Total, invoice.Settlement, invoice.AmountPaid, invoice.AmountCredited, invoice.AmountDue));

        var creditNote = Assert.Single(await CreditNotesAsync(harness, invoice.ItemId));
        Assert.Equal(CreditNoteDisposition.ReduceBalance, creditNote.Disposition);
        Assert.Equal([(10m, 10m, 1m), (20m, 5m, 1m)], creditNote.Taxes.Where(tax => tax.Rate > 0m).OrderBy(tax => tax.Rate).Select(tax => (tax.Rate, tax.TaxableAmount, tax.TaxAmount)));
        Assert.Equal(TransactionStatus.Paid, Assert.Single(await DebtsAsync(harness)).Status);
    }

    [Fact]
    public async Task AGiftCardThatPaidPartOfTheOrder_IsRecordedOnTheInvoice_SoItSettles()
    {
        // Arrange: a $100 invoice; at the order's checkout a $30 gift card paid part and the card the rest.
        await using var harness = await CreateHarnessAsync();
        var order = NewDraftOrder();
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));
        order.Tenders = [new OrderTender { ProviderKey = "GiftCard", Reference = "GC-1", Description = "Gift card GC-1", Amount = 30m }];

        // Act
        await PayOrderAsync(harness, order, 70m);

        // Assert
        var invoice = await OnBridgeAsync(harness, bridge => bridge.FindInvoiceAsync(order.ItemId, TestContext.Current.CancellationToken));
        Assert.Equal((InvoiceSettlement.Paid, 100m, 0m), (invoice.Settlement, invoice.AmountPaid, invoice.AmountCredited));
        Assert.Equal((TransactionStatus.Paid, 100m), (Assert.Single(await DebtsAsync(harness)).Status, Assert.Single(await DebtsAsync(harness)).AmountPaid));
    }

    [Fact]
    public async Task APartRefundOfTheOrder_IsCreditedWithTheTaxItsPaymentsSnapshotGivesIt_ByRate()
    {
        // Arrange: the $170 invoice paid in full, the payment's tax snapshot taxing $100 at 10% and $50 at 20%.
        await using var harness = await CreateHarnessAsync(taxed: true);
        var order = NewTaxedDraftOrder();
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));
        await PayOrderAsync(harness, order, 170m, new TaxSnapshot
        {
            Currency = "USD",
            TaxableAmount = 150m,
            TaxAmount = 20m,
            TotalAmount = 170m,
            Lines =
            [
                new TaxLine { ItemId = "l1", TaxCode = "VAT10", TaxName = "VAT", Rate = 0.1m, TaxableAmount = 100m, TaxAmount = 10m, Treatment = TaxTreatment.Taxable },
                new TaxLine { ItemId = "l2", TaxCode = "VAT20", TaxName = "VAT", Rate = 0.2m, TaxableAmount = 50m, TaxAmount = 10m, Treatment = TaxTreatment.Taxable },
            ],
        });

        // Act: half the order is refunded ($85).
        await harness.InScopeAsync(async services =>
        {
            foreach (var handler in services.GetServices<IOrderEventHandler>())
            {
                await handler.RefundedAsync(new OrderRefundedContext(order, new OrderRefund { Amount = 85m }), TestContext.Current.CancellationToken);
            }

            return true;
        });

        // Assert: credited $85 as lines by rate, the tax from the snapshot: $50 at 10% ($5) and $25 at 20% ($5).
        var invoice = await OnBridgeAsync(harness, bridge => bridge.FindInvoiceAsync(order.ItemId, TestContext.Current.CancellationToken));
        var creditNote = Assert.Single(await CreditNotesAsync(harness, invoice.ItemId));
        Assert.Equal((CreditNoteDisposition.RefundedElsewhere, 85m), (creditNote.Disposition, creditNote.Totals.Total));
        Assert.Equal([(10m, 50m, 5m), (20m, 25m, 5m)], creditNote.Taxes.Where(tax => tax.Rate > 0m).OrderBy(tax => tax.Rate).Select(tax => (tax.Rate, tax.TaxableAmount, tax.TaxAmount)));
        Assert.Equal(85m, invoice.AmountCredited);
    }

    [Fact]
    public async Task AFullRefundOfATaxedOrder_CreditsTheWholeInvoiceLineForLine()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync(taxed: true);
        var order = NewTaxedDraftOrder();
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));
        await PayOrderAsync(harness, order, 170m);

        // Act
        await harness.InScopeAsync(async services =>
        {
            foreach (var handler in services.GetServices<IOrderEventHandler>())
            {
                await handler.RefundedAsync(new OrderRefundedContext(order, new OrderRefund { Amount = 170m }), TestContext.Current.CancellationToken);
            }

            return true;
        });

        // Assert: each invoice line credited as it was invoiced.
        var invoice = await OnBridgeAsync(harness, bridge => bridge.FindInvoiceAsync(order.ItemId, TestContext.Current.CancellationToken));
        var creditNote = Assert.Single(await CreditNotesAsync(harness, invoice.ItemId));
        Assert.Equal(invoice.Lines.Select(line => line.Id), creditNote.Lines.Select(line => line.CreditedLineId));
        Assert.Equal((170m, 170m), (creditNote.Totals.Total, invoice.AmountCredited));
    }

    [Fact]
    public async Task ADraftOrderWithNoCurrencyYet_IsInvoicedInTheSitesDefaultCurrency_WithTaxRulesOn()
    {
        // Arrange: a draft order put together in the admin has no currency until its checkout; tax rules are on.
        await using var harness = await CreateHarnessAsync(taxed: true, settings: new InvoicingSettings { DefaultCurrency = "USD" });
        var order = NewTaxedDraftOrder();
        order.Currency = null;
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var sent = await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(sent.Sent, string.Join(" ", sent.Errors));
        var invoice = await OnBridgeAsync(harness, bridge => bridge.FindInvoiceAsync(order.ItemId, TestContext.Current.CancellationToken));
        Assert.Equal(("USD", InvoiceStatus.Open, 170m), (invoice.Currency, invoice.Status, invoice.Totals.Total));
    }

    [Fact]
    public async Task SendingTheOrderAgain_SendsTheSameInvoice_WithoutASecondOne()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        harness.CanSendEmail = true;
        var order = NewDraftOrder();
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var first = await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));
        var second = await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(first.Sent && second.Sent, string.Join(" ", first.Errors.Concat(second.Errors)));
        Assert.Equal(2, harness.Emails.Count);
        Assert.Single(await DebtsAsync(harness));
        Assert.Single((await harness.InScopeAsync(services => services.GetRequiredService<IInvoiceManager>().PageAsync(1, 10, new InvoiceQuery { SourceType = InvoicingConstants.OrderReferenceType, SourceId = order.ItemId }, TestContext.Current.CancellationToken))).Entries);
    }

    [Fact]
    public async Task AQuoteAcceptedFromItsEstimate_EndsWithTheOrderPaid_AndTheEstimateInvoiced()
    {
        // Arrange: a quote the store priced at $100 and sends; e-mail isn't set up, so the estimate's link is shared.
        await using var harness = await CreateHarnessAsync();
        harness.CanSendEmail = false;
        var order = NewDraftOrder();
        order.Quote = new OrderQuote { RequestedUtc = InvoicingTestHarness.Now, Message = "Price 1 widget", Status = OrderQuoteStatus.Requested };
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act: sent as an estimate (the order's quote is then sent, as the order's send action records), accepted on the
        // estimate's page, which sends the buyer to the order's pay page, and the order paid there.
        var sent = await harness.InScopeAsync(services => services.GetServices<IOrderDocumentSender>().Single().SendAsync(order, TestContext.Current.CancellationToken));
        order.Quote.Status = OrderQuoteStatus.Sent;

        var estimate = await OnBridgeAsync(harness, bridge => bridge.FindEstimateAsync(order, TestContext.Current.CancellationToken));
        var accepted = await harness.InScopeAsync(services => services.GetRequiredService<IEstimateCommandService>().AcceptAsync(estimate.ItemId, new EstimateAcceptanceRequest { Name = "Pat Doe", Today = DateOnly.FromDateTime(InvoicingTestHarness.Now) }, TestContext.Current.CancellationToken));
        var payUrl = await harness.InScopeAsync(services => services.GetServices<IEstimatePayLinkProvider>().Single().GetPayUrlAsync(accepted.Estimate, TestContext.Current.CancellationToken));

        await PayOrderAsync(harness, order, 100m);

        // Assert: made but not e-mailed, as e-mail isn't set up.
        Assert.True(sent.Sent, string.Join(" ", sent.Errors));
        Assert.Equal((false, estimate.Number), (sent.Emailed, sent.DocumentNumber));
        Assert.Equal((EstimateConversion.None, 100m), (estimate.OnAcceptance, estimate.Totals.Total));
        Assert.True(accepted.Succeeded, string.Join(" ", accepted.Errors));
        Assert.Equal(OrderQuoteStatus.Accepted, order.Quote.Status);
        Assert.Equal(PayUrlPrefix + order.ItemId, payUrl);

        var invoice = await OnBridgeAsync(harness, bridge => bridge.FindInvoiceAsync(order.ItemId, TestContext.Current.CancellationToken));
        Assert.Equal((InvoiceStatus.Open, InvoiceSettlement.Paid), (invoice.Status, invoice.Settlement));

        var invoiced = await OnBridgeAsync(harness, bridge => bridge.FindEstimateAsync(order, TestContext.Current.CancellationToken));
        Assert.Equal(EstimateStatus.Invoiced, invoiced.Status);
        Assert.Equal([invoice.ItemId], invoiced.InvoiceIds);
        Assert.Single(await DebtsAsync(harness));
    }

    [Fact]
    public async Task WithInvoicingEstimatesOff_AQuoteKeepsTheOrdersOwnEmail()
    {
        // Arrange: the bridge on a site where Invoicing Estimates is off.
        await using var harness = await CreateHarnessAsync();
        var order = NewDraftOrder();
        order.Quote = new OrderQuote { RequestedUtc = InvoicingTestHarness.Now, Status = OrderQuoteStatus.Requested };

        // Act
        var result = await harness.InScopeAsync(services =>
        {
            var withoutEstimates = new WithoutService<IEstimateCommandService>(services);
            var bridge = ActivatorUtilities.CreateInstance<OrderDocumentBridge>(withoutEstimates, (IServiceProvider)withoutEstimates);

            return new InvoicingOrderDocumentSender(bridge).SendAsync(order, TestContext.Current.CancellationToken);
        });

        // Assert: not handled, so Orders sends its own quote e-mail.
        Assert.Null(result);
        Assert.Empty(await DebtsAsync(harness));
    }

    [Fact]
    public async Task CancelingTheOrder_VoidsItsUnpaidInvoice()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var order = NewDraftOrder();
        harness.Orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        await OnBridgeAsync(harness, bridge => bridge.SendInvoiceAsync(order, TestContext.Current.CancellationToken));

        // Act
        order.Status = OrderStatus.Canceled;
        await harness.InScopeAsync(async services =>
        {
            foreach (var handler in services.GetServices<IOrderEventHandler>())
            {
                await handler.CanceledAsync(new OrderCanceledContext(order, OrderCancelReason.CanceledByStore), TestContext.Current.CancellationToken);
            }

            return true;
        });

        // Assert
        Assert.Null(await OnBridgeAsync(harness, bridge => bridge.FindInvoiceAsync(order.ItemId, TestContext.Current.CancellationToken)));
        Assert.All(await DebtsAsync(harness), debt => Assert.Equal(TransactionStatus.Canceled, debt.Status));
    }

    private static Order NewDraftOrder()
        => new()
        {
            ItemId = "order-42",
            OrderNumber = "1042",
            Currency = "USD",
            Status = OrderStatus.Draft,
            Total = 100m,
            CustomerName = "Pat Doe",
            CustomerEmail = "pat@example.com",
            Lines = [new OrderLine { LineId = "l1", Title = "Widget", Sku = "W-1", Quantity = 2, UnitPrice = 50m, LineTotal = 100m }],
            Invoice = new OrderInvoice { CreatedBy = "admin", DueUtc = InvoicingTestHarness.Now.AddDays(14) },
        };

    // A widget and a setup service, taxed at 10% and 20% by the test's tax rules.
    private static Order NewTaxedDraftOrder()
        => new()
        {
            ItemId = "order-43",
            OrderNumber = "1043",
            Currency = "USD",
            Status = OrderStatus.Draft,
            Total = 170m,
            CustomerName = "Pat Doe",
            CustomerEmail = "pat@example.com",
            Lines =
            [
                new OrderLine { LineId = "l1", Title = "Widget", Sku = "W-1", Quantity = 1, UnitPrice = 100m, LineTotal = 100m, ProductType = ProductType.Good },
                new OrderLine { LineId = "l2", Title = "Setup", Sku = "S-1", Quantity = 1, UnitPrice = 50m, LineTotal = 50m, ProductType = ProductType.Service },
            ],
            Invoice = new OrderInvoice { CreatedBy = "admin", DueUtc = InvoicingTestHarness.Now.AddDays(14) },
        };

    private static Task<IReadOnlyList<CreditNote>> CreditNotesAsync(InvoicingTestHarness harness, string invoiceId)
        => harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteStore>().ListByInvoiceAsync(invoiceId, TestContext.Current.CancellationToken));

    private static Task<InvoicingTestHarness> CreateHarnessAsync(bool withBridge = true, bool taxed = false, InvoicingSettings settings = null)
        => InvoicingTestHarness.CreateAsync(settings, configureServices: services =>
        {
            if (taxed)
            {
                services.AddSingleton<IDocumentTaxProvider, KindTaxProvider>();
                services.AddSingleton<ITaxRefundCalculator>(new DefaultTaxRefundCalculator(new DefaultTaxRoundingStrategy(Options.Create(new TaxationOptions()))));
            }

            var links = new Mock<IOrderLinkService>();
            links.Setup(service => service.CreatePayUrlAsync(It.IsAny<Order>())).ReturnsAsync((Order order) => PayUrlPrefix + order.ItemId);

            services.AddSingleton(links.Object);
            services.AddSingleton(Mock.Of<IOrderService>());
            services.AddSingleton<IPaymentAttemptStore>(new InMemoryPaymentAttemptStore());
            services.AddScoped<ITransactionSettlementService>(sp => new TransactionSettlementService(
                sp.GetRequiredService<ITransactionManager>(),
                sp,
                sp.GetRequiredService<IClock>(),
                NullLogger<TransactionSettlementService>.Instance,
                new PassThroughStringLocalizer<TransactionSettlementService>()));
            services.AddScoped<ITransactionPayLinkService>(sp => new FixedTransactionPayLinks(sp));

            if (withBridge)
            {
                new OrderDocumentsStartup().ConfigureServices(services);
            }
        });

    private static Task<T> OnBridgeAsync<T>(InvoicingTestHarness harness, Func<OrderDocumentBridge, Task<T>> work)
        => harness.InScopeAsync(services => work(services.GetRequiredService<OrderDocumentBridge>()));

    // The order's own checkout took the money; the order is marked paid and its handlers run, as Orders does.
    private static async Task PayOrderAsync(InvoicingTestHarness harness, Order order, decimal amount, TaxSnapshot taxSnapshot = null)
    {
        order.CheckoutSessionId = "checkout-" + order.ItemId;
        order.Status = OrderStatus.Paid;

        await harness.InScopeAsync(async services =>
        {
            await services.GetRequiredService<IPaymentAttemptStore>().CreateAsync(new PaymentAttempt
            {
                ItemId = "attempt-" + order.ItemId,
                SessionId = order.CheckoutSessionId,
                ReferenceType = "Order",
                ReferenceId = order.ItemId,
                ProviderKey = "test",
                ProviderReference = "pi_" + order.ItemId,
                ObligationId = "obligation-" + order.ItemId,
                Currency = "USD",
                ConfirmedAmount = amount,
                State = PaymentAttemptState.Succeeded,
                TaxSnapshot = taxSnapshot,
            }, TestContext.Current.CancellationToken);

            foreach (var handler in services.GetServices<IOrderEventHandler>())
            {
                await handler.PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);
            }

            return true;
        });
    }

    private static Task<IReadOnlyList<Transaction>> DebtsAsync(InvoicingTestHarness harness)
        => harness.InScopeAsync(async services =>
        {
            var page = await services.GetRequiredService<ITransactionStore>().PageAsync(1, 50, new TransactionQuery(), TestContext.Current.CancellationToken);

            return (IReadOnlyList<Transaction>)[.. page.Entries.Where(entry => entry.ReferenceType == InvoicingConstants.InvoiceReferenceType)];
        });

    // The ledger's pay links as the Transactions module makes them: the page of its own a provider names, or the ledger's.
    private sealed class KindTaxProvider : IDocumentTaxProvider
    {
        public int Order => 0;

        public Task<bool> ApplyTaxesAsync(DocumentCalculationContext context, CancellationToken cancellationToken = default)
        {
            // As the Taxation module's tax service does.
            if (string.IsNullOrWhiteSpace(context.Currency))
            {
                throw new InvalidOperationException("A tax calculation context must specify the currency the amounts are expressed in.");
            }

            foreach (var line in context.Lines ?? [])
            {
                if (context.KeepTypedTaxes && line.Taxes is { Count: > 0 })
                {
                    continue;
                }

                line.Taxes = line.Kind switch
                {
                    DocumentLineKind.Item => [new DocumentLineTax { Treatment = TaxTreatment.Taxable, Code = "VAT10", Name = "VAT", Rate = 10m }],
                    DocumentLineKind.Service => [new DocumentLineTax { Treatment = TaxTreatment.Taxable, Code = "VAT20", Name = "VAT", Rate = 20m }],
                    _ => line.Taxes ?? [],
                };
            }

            return Task.FromResult(true);
        }
    }

    private sealed class FixedTransactionPayLinks : ITransactionPayLinkService
    {
        private readonly IServiceProvider _services;

        public FixedTransactionPayLinks(IServiceProvider services)
        {
            _services = services;
        }

        public async Task<string> CreatePayUrlAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            foreach (var provider in _services.GetServices<ITransactionPayUrlProvider>())
            {
                if (await provider.GetPayUrlAsync(transaction, cancellationToken) is { } url)
                {
                    return url;
                }
            }

            return "https://shop.example/transactions/pay/" + transaction.ItemId;
        }

        public string GetTransactionId(string token)
            => token;
    }

    // A provider that hides one service, as on a site where the feature that registers it is off.
    private sealed class WithoutService<THidden> : IServiceProvider
    {
        private readonly IServiceProvider _inner;

        public WithoutService(IServiceProvider inner)
        {
            _inner = inner;
        }

        public object GetService(Type serviceType)
            => serviceType == typeof(THidden) ? null : _inner.GetService(serviceType);
    }
}
