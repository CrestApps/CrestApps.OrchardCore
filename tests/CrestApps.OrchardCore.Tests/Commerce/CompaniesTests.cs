using System.Security.Claims;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// A company buyer pays a deposit at checkout and puts the rest on the company's account, up to its credit limit, and
/// gives a purchase order number when the company asks for one.
/// </summary>
public sealed class CompaniesTests
{
    private const string BuyerId = "buyer-1";

    [Fact]
    public void AnAccountPaymentsReference_NamesTheCompanyAndTheCheckout()
    {
        var reference = CompanyTermsService.ToReference("company-1", "session-1");

        Assert.Equal(("company-1", "session-1"), CompanyTermsService.FromReference(reference));
        Assert.Equal((null, null), CompanyTermsService.FromReference("nonsense"));
    }

    [Theory]
    [InlineData(20, null, 48)]
    [InlineData(0, null, 60)]
    [InlineData(100, null, 0)]
    [InlineData(20, 30.0, 30)]
    public async Task WhatGoesOnAccount_IsTheOrderLessItsDeposit_UpToTheCreditLeft(decimal depositPercent, double? creditLimit, decimal onAccount)
    {
        var company = Company(new CompanyTerms { NetDays = 30, DepositPercent = depositPercent, CreditLimit = (decimal?)creditLimit });
        var terms = CreateTerms(company);

        var (found, amount) = await terms.GetForCheckoutAsync(Flow(), new CheckoutInvoice { Currency = "USD", GrandTotal = 60m }, TestContext.Current.CancellationToken);

        Assert.Same(company, found);
        Assert.Equal(onAccount, amount);
    }

    [Fact]
    public async Task ACompanyWithoutTerms_OrAnOrderInAnotherCurrency_PaysAtCheckout()
    {
        var withoutTerms = CreateTerms(Company(new CompanyTerms()));
        var withTerms = CreateTerms(Company(new CompanyTerms { NetDays = 30 }));

        Assert.Null((await withoutTerms.GetForCheckoutAsync(Flow(), new CheckoutInvoice { Currency = "USD", GrandTotal = 60m }, TestContext.Current.CancellationToken)).Company);
        Assert.Null((await withTerms.GetForCheckoutAsync(Flow(), new CheckoutInvoice { Currency = "EUR", GrandTotal = 60m }, TestContext.Current.CancellationToken)).Company);
    }

    [Fact]
    public async Task ARegistersSale_IsntPaidOnAccount()
    {
        var terms = CreateTerms(Company(new CompanyTerms { NetDays = 30 }));
        var flow = Flow();
        flow.Session.Put(new CheckoutCounterSale { RegisterId = "register-1" });

        Assert.Null((await terms.GetForCheckoutAsync(flow, new CheckoutInvoice { Currency = "USD", GrandTotal = 60m }, TestContext.Current.CancellationToken)).Company);
    }

    [Fact]
    public async Task ACompanyThatAsksForAPurchaseOrderNumber_DoesntCheckOutWithoutOne()
    {
        var company = Company(new CompanyTerms { RequirePurchaseOrder = true });
        var order = new Order { ItemId = "order-1" };
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var step = new CompanyCheckoutStepService(Companies(company), orders.Object, Approvals(orders.Object), new PassThroughStringLocalizer<CompanyCheckoutStepService>());
        var session = Flow().Session as CheckoutSession;

        var missing = await step.SaveAsync(session, " ", TestContext.Current.CancellationToken);
        var saved = await step.SaveAsync(session, " PO-77812 ", TestContext.Current.CancellationToken);

        Assert.Contains("purchaseOrderNumber", missing.Keys);
        Assert.Empty(saved);
        Assert.Equal("PO-77812", order.PurchaseOrderNumber);
        Assert.Equal("company-1", order.CompanyId);
        Assert.Equal("PO-77812", CompanyCheckoutStepService.GetPurchaseOrderNumber(session));
    }

    [Fact]
    public async Task AnOrderOverABuyersLimit_OrPastTheMonthlyBudget_NeedsApproval_ButNotAnAdmins()
    {
        var company = Company(new CompanyTerms());
        var buyer = new CompanyBuyer { UserId = "buyer-2", Role = CompanyBuyerRole.Buyer, OrderLimit = 50m };
        var admin = company.Buyers[0];
        var orders = PaidThisMonth(80m);
        var approvals = Approvals(orders.Object);

        Assert.Contains("order limit", await approvals.GetReasonAsync(company, buyer, 60m, "USD", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await approvals.GetReasonAsync(company, buyer, 50m, "USD", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await approvals.GetReasonAsync(company, admin, 600m, "USD", cancellationToken: TestContext.Current.CancellationToken));

        company.MonthlyBudget = 100m;

        Assert.Equal(80m, await approvals.GetSpentThisMonthAsync(company, "USD", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("monthly budget", await approvals.GetReasonAsync(company, buyer, 30m, "USD", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await approvals.GetReasonAsync(company, buyer, 20m, "USD", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheCompanyStep_SendsAnOrderForApproval_AndPassesOnceItsApproved()
    {
        var company = Company(new CompanyTerms());
        company.Buyers.Add(new CompanyBuyer { UserId = "buyer-2", UserName = "sam", Role = CompanyBuyerRole.Buyer, OrderLimit = 50m });

        var order = new Order { ItemId = "order-1", OrderNumber = "261020-APPRV1", Status = OrderStatus.AwaitingPayment };
        var orders = PaidThisMonth(0m);
        orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var approvals = Approvals(orders.Object);
        var step = new CompanyCheckoutStepService(Companies(company, "buyer-2"), orders.Object, approvals, new PassThroughStringLocalizer<CompanyCheckoutStepService>());
        var session = Flow("buyer-2").Session as CheckoutSession;
        session.Put(new CheckoutInvoice { Currency = "USD", GrandTotal = 60m });

        Assert.Contains("approval", (await step.SaveAsync(session, null, requestApproval: false, TestContext.Current.CancellationToken)).Keys);
        Assert.Null(order.Approval);

        Assert.Contains("approval", (await step.SaveAsync(session, null, requestApproval: true, TestContext.Current.CancellationToken)).Keys);
        Assert.Equal(OrderApprovalStatus.Pending, order.Approval.Status);
        Assert.Equal(60m, order.Approval.Amount);
        Assert.NotNull(order.Invoice);
        Assert.False(CrestApps.OrchardCore.Orders.Services.QuoteService.CanPay(order, DateTime.UtcNow));
        Assert.Null(CompanyCheckoutStepService.GetPurchaseOrderNumber(session));

        Assert.True(await approvals.DecideAsync(order, approve: true, "admin", "OK for this one", TestContext.Current.CancellationToken));
        Assert.True(CrestApps.OrchardCore.Orders.Services.QuoteService.CanPay(order, DateTime.UtcNow));
        Assert.Empty(await step.SaveAsync(session, "PO-1", requestApproval: false, TestContext.Current.CancellationToken));

        // An order that grows past what was approved needs approval again.
        session.Put(new CheckoutInvoice { Currency = "USD", GrandTotal = 75m });

        Assert.Contains("approval", (await step.SaveAsync(session, "PO-1", requestApproval: false, TestContext.Current.CancellationToken)).Keys);
    }

    private static Mock<IOrderStore> PaidThisMonth(decimal total)
    {
        var orders = new Mock<IOrderStore>();
        IReadOnlyList<Order> paid = total > 0m ? [new Order { ItemId = "paid-1", Currency = "USD", Subtotal = total, Total = total, Status = OrderStatus.Paid }] : [];
        orders.Setup(store => store.PageAsync(It.IsAny<OrderQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((paid.Count, paid));

        return orders;
    }

    private static CompanyApprovalService Approvals(IOrderStore orders)
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.CancelAsync(It.IsAny<Order>(), It.IsAny<OrderCancelReason>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var clock = new Mock<global::OrchardCore.Modules.IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(DateTime.UtcNow);

        return new CompanyApprovalService(
            orders,
            orderService.Object,
            SiteService(),
            Mock.Of<IServiceProvider>(),
            clock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CompanyApprovalService>.Instance,
            new PassThroughStringLocalizer<CompanyApprovalService>());
    }

    private static ISiteService SiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private static Company Company(CompanyTerms terms)
        => new()
        {
            ItemId = "company-1",
            Name = "Fabrikam Ltd",
            Terms = terms,
            Buyers = [new CompanyBuyer { UserId = BuyerId, Role = CompanyBuyerRole.Admin }],
        };

    private static CheckoutFlow Flow(string ownerId = BuyerId)
        => new(new CheckoutSession
        {
            SessionId = "session-1",
            OwnerId = ownerId,
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = "order-1",
        });

    private static CompanyService Companies(Company company, string userId = BuyerId)
    {
        var store = new Mock<ICompanyStore>();
        store.Setup(value => value.FindByBuyerAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        store.Setup(value => value.FindByIdAsync(company.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test")) };

        return new CompanyService(store.Object, new HttpContextAccessor { HttpContext = httpContext });
    }

    private static CompanyTermsService CreateTerms(Company company)
    {
        var transactions = new Mock<ITransactionManager>();
        transactions.Setup(value => value.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TransactionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrestApps.Core.Models.PageResult<CrestApps.OrchardCore.Transactions.Models.Transaction> { Entries = [] });

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new CompanyTermsService(transactions.Object, Companies(company), siteService.Object);
    }
}
