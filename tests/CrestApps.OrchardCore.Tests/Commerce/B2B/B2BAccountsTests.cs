using System.Security.Claims;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// A account buyer pays a deposit at checkout and puts the rest on the account's account, up to its credit limit, and
/// gives a purchase order number when the account asks for one.
/// </summary>
public sealed class B2BAccountsTests
{
    private const string BuyerId = "buyer-1";

    [Fact]
    public void AnAccountPaymentsReference_NamesTheAccountAndTheCheckout()
    {
        var reference = AccountTermsService.ToReference("account-1", "session-1");

        Assert.Equal(("account-1", "session-1"), AccountTermsService.FromReference(reference));
        Assert.Equal((null, null), AccountTermsService.FromReference("nonsense"));
    }

    [Theory]
    [InlineData(20, null, 48)]
    [InlineData(0, null, 60)]
    [InlineData(100, null, 0)]
    [InlineData(20, 30.0, 30)]
    public async Task WhatGoesOnAccount_IsTheOrderLessItsDeposit_UpToTheCreditLeft(decimal depositPercent, double? creditLimit, decimal onAccount)
    {
        var account = Account(new AccountTerms { NetDays = 30, DepositPercent = depositPercent, CreditLimit = (decimal?)creditLimit });
        var terms = CreateTerms(account);

        var (found, amount) = await terms.GetForCheckoutAsync(Flow(), new CheckoutInvoice { Currency = "USD", GrandTotal = 60m }, TestContext.Current.CancellationToken);

        Assert.Same(account, found);
        Assert.Equal(onAccount, amount);
    }

    [Fact]
    public async Task AnAccountWithoutTerms_OrAnOrderInAnotherCurrency_PaysAtCheckout()
    {
        var withoutTerms = CreateTerms(Account(new AccountTerms()));
        var withTerms = CreateTerms(Account(new AccountTerms { NetDays = 30 }));

        Assert.Null((await withoutTerms.GetForCheckoutAsync(Flow(), new CheckoutInvoice { Currency = "USD", GrandTotal = 60m }, TestContext.Current.CancellationToken)).Account);
        Assert.Null((await withTerms.GetForCheckoutAsync(Flow(), new CheckoutInvoice { Currency = "EUR", GrandTotal = 60m }, TestContext.Current.CancellationToken)).Account);
    }

    [Fact]
    public async Task ARegistersSale_IsntPaidOnAccount()
    {
        var terms = CreateTerms(Account(new AccountTerms { NetDays = 30 }));
        var flow = Flow();
        flow.Session.Put(new CheckoutCounterSale { RegisterId = "register-1" });

        Assert.Null((await terms.GetForCheckoutAsync(flow, new CheckoutInvoice { Currency = "USD", GrandTotal = 60m }, TestContext.Current.CancellationToken)).Account);
    }

    [Fact]
    public async Task AnAccountThatAsksForAPurchaseOrderNumber_DoesntCheckOutWithoutOne()
    {
        var account = Account(new AccountTerms { RequirePurchaseOrder = true });
        var order = new Order { ItemId = "order-1" };
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var step = new B2BCheckoutStepService(B2BAccounts(account), orders.Object, Approvals(orders.Object), new PassThroughStringLocalizer<B2BCheckoutStepService>());
        var session = Flow().Session as CheckoutSession;

        var missing = await step.SaveAsync(session, " ", TestContext.Current.CancellationToken);
        var saved = await step.SaveAsync(session, " PO-77812 ", TestContext.Current.CancellationToken);

        Assert.Contains("purchaseOrderNumber", missing.Keys);
        Assert.Empty(saved);
        Assert.Equal("PO-77812", order.PurchaseOrderNumber);
        Assert.Equal("account-1", order.AccountId);
        Assert.Equal("PO-77812", B2BCheckoutStepService.GetPurchaseOrderNumber(session));
    }

    [Fact]
    public async Task AnOrderOverABuyersLimit_OrPastTheMonthlyBudget_NeedsApproval_ButNotAnAdmins()
    {
        var account = Account(new AccountTerms());
        var buyer = new AccountBuyer { UserId = "buyer-2", Role = AccountMemberRole.Member, OrderLimit = 50m };
        var admin = account.Buyers[0];
        var orders = PaidThisMonth(80m);
        var approvals = Approvals(orders.Object);

        Assert.Contains("order limit", await approvals.GetReasonAsync(account, buyer, 60m, "USD", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await approvals.GetReasonAsync(account, buyer, 50m, "USD", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await approvals.GetReasonAsync(account, admin, 600m, "USD", cancellationToken: TestContext.Current.CancellationToken));

        account.Part.MonthlyBudget = 100m;

        Assert.Equal(80m, await approvals.GetSpentThisMonthAsync(account, "USD", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("monthly budget", await approvals.GetReasonAsync(account, buyer, 30m, "USD", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await approvals.GetReasonAsync(account, buyer, 20m, "USD", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheBusinessAccountStep_SendsAnOrderForApproval_AndPassesOnceItsApproved()
    {
        var account = TestB2BAccounts.Create(
            configure: part =>
            {
                part.Terms = new AccountTerms();
                part.OrderLimits = [new BuyerOrderLimit { UserId = "buyer-2", Limit = 50m }];
            },
            members:
            [
                new AccountMember { UserId = BuyerId, Role = AccountMemberRole.Admin },
                new AccountMember { UserId = "buyer-2", UserName = "sam", Role = AccountMemberRole.Member },
            ]);

        var order = new Order { ItemId = "order-1", OrderNumber = "261020-APPRV1", Status = OrderStatus.AwaitingPayment };
        var orders = PaidThisMonth(0m);
        orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var approvals = Approvals(orders.Object);
        var step = new B2BCheckoutStepService(B2BAccounts(account, "buyer-2"), orders.Object, approvals, new PassThroughStringLocalizer<B2BCheckoutStepService>());
        var session = Flow("buyer-2").Session as CheckoutSession;
        session.Put(new CheckoutInvoice { Currency = "USD", GrandTotal = 60m });

        Assert.Contains("approval", (await step.SaveAsync(session, null, requestApproval: false, TestContext.Current.CancellationToken)).Keys);
        Assert.Null(order.Approval);

        Assert.Contains("approval", (await step.SaveAsync(session, null, requestApproval: true, TestContext.Current.CancellationToken)).Keys);
        Assert.Equal(OrderApprovalStatus.Pending, order.Approval.Status);
        Assert.Equal(60m, order.Approval.Amount);
        Assert.NotNull(order.Invoice);
        Assert.False(CrestApps.OrchardCore.Orders.Services.QuoteService.CanPay(order, DateTime.UtcNow));
        Assert.Null(B2BCheckoutStepService.GetPurchaseOrderNumber(session));

        Assert.True(await approvals.DecideAsync(order, approve: true, "admin", "OK for this one", TestContext.Current.CancellationToken));
        Assert.True(CrestApps.OrchardCore.Orders.Services.QuoteService.CanPay(order, DateTime.UtcNow));
        Assert.Empty(await step.SaveAsync(session, "PO-1", requestApproval: false, TestContext.Current.CancellationToken));

        // An order that grows past what was approved needs approval again.
        session.Put(new CheckoutInvoice { Currency = "USD", GrandTotal = 75m });

        Assert.Contains("approval", (await step.SaveAsync(session, "PO-1", requestApproval: false, TestContext.Current.CancellationToken)).Keys);
    }

    [Fact]
    public async Task ASaleAtARegister_IsntABusinessOrder_EvenWhenTheCashierBuysForOne()
    {
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(new Order { ItemId = "order-1", Channel = OrderChannels.PointOfSale });

        var step = new B2BCheckoutStepService(B2BAccounts(Account(new AccountTerms())), orders.Object, Approvals(orders.Object), new PassThroughStringLocalizer<B2BCheckoutStepService>());

        Assert.Null(await step.GetB2BAccountAsync(Flow().Session, TestContext.Current.CancellationToken));
    }

    private static Mock<IOrderStore> PaidThisMonth(decimal total)
    {
        var orders = new Mock<IOrderStore>();
        IReadOnlyList<Order> paid = total > 0m ? [new Order { ItemId = "paid-1", Currency = "USD", Subtotal = total, Total = total, Status = OrderStatus.Paid }] : [];
        orders.Setup(store => store.PageAsync(It.IsAny<OrderQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((paid.Count, paid));

        return orders;
    }

    private static B2BApprovalService Approvals(IOrderStore orders)
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.CancelAsync(It.IsAny<Order>(), It.IsAny<OrderCancelReason>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var clock = new Mock<global::OrchardCore.Modules.IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(DateTime.UtcNow);

        return new B2BApprovalService(
            orders,
            orderService.Object,
            SiteService(),
            Mock.Of<IServiceProvider>(),
            clock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<B2BApprovalService>.Instance,
            new PassThroughStringLocalizer<B2BApprovalService>(),
            Mock.Of<IShopperCurrency>(),
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<LinkGenerator>());
    }

    private static ISiteService SiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private static B2BAccount Account(AccountTerms terms)
        => TestB2BAccounts.Create(configure: part => part.Terms = terms, members: [new AccountMember { UserId = BuyerId, Role = AccountMemberRole.Admin }]);

    private static CheckoutFlow Flow(string ownerId = BuyerId)
        => new(new CheckoutSession
        {
            SessionId = "session-1",
            OwnerId = ownerId,
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = "order-1",
        });

    private static B2BAccountService B2BAccounts(B2BAccount account, string userId = BuyerId)
    {
        var store = new Mock<IB2BAccountStore>();
        store.Setup(value => value.FindByBuyerAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        store.Setup(value => value.FindByIdAsync(account.AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test")) };

        return new B2BAccountService(store.Object, new HttpContextAccessor { HttpContext = httpContext });
    }

    private static AccountTermsService CreateTerms(B2BAccount account)
    {
        var transactions = new Mock<ITransactionManager>();
        transactions.Setup(value => value.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TransactionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrestApps.Core.Models.PageResult<CrestApps.OrchardCore.Transactions.Models.Transaction> { Entries = [] });

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new AccountTermsService(transactions.Object, B2BAccounts(account), siteService.Object);
    }
}
