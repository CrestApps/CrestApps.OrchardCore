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
        var step = new CompanyCheckoutStepService(Companies(company), orders.Object, new PassThroughStringLocalizer<CompanyCheckoutStepService>());
        var session = Flow().Session as CheckoutSession;

        var missing = await step.SaveAsync(session, " ", TestContext.Current.CancellationToken);
        var saved = await step.SaveAsync(session, " PO-77812 ", TestContext.Current.CancellationToken);

        Assert.Contains("purchaseOrderNumber", missing.Keys);
        Assert.Empty(saved);
        Assert.Equal("PO-77812", order.PurchaseOrderNumber);
        Assert.Equal("company-1", order.CompanyId);
        Assert.Equal("PO-77812", CompanyCheckoutStepService.GetPurchaseOrderNumber(session));
    }

    private static Company Company(CompanyTerms terms)
        => new()
        {
            ItemId = "company-1",
            Name = "Fabrikam Ltd",
            Terms = terms,
            Buyers = [new CompanyBuyer { UserId = BuyerId, Role = CompanyBuyerRole.Admin }],
        };

    private static CheckoutFlow Flow()
        => new(new CheckoutSession
        {
            SessionId = "session-1",
            OwnerId = BuyerId,
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = "order-1",
        });

    private static CompanyService Companies(Company company)
    {
        var store = new Mock<ICompanyStore>();
        store.Setup(value => value.FindByBuyerAsync(BuyerId, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        store.Setup(value => value.FindByIdAsync(company.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, BuyerId)], "test")) };

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
