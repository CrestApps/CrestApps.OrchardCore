using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Controllers;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The pay-link page starts the checkout that pays its transaction, and passes itself as the place the customer
/// returns to, so they come back to it after paying or when they decide not to pay.
/// </summary>
public sealed class TransactionPayControllerTests
{
    [Fact]
    public async Task Pay_StartsTheCheckoutWithThePayLinkPageAsItsReturnUrl()
    {
        // Arrange
        StartCheckoutRequest started = null;

        var engine = new Mock<ICheckoutEngine>();
        engine
            .Setup(e => e.StartAsync(It.IsAny<StartCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .Callback<StartCheckoutRequest, CancellationToken>((request, _) => started = request)
            .ReturnsAsync(new CheckoutSession { SessionId = "session-1", CurrentStep = CheckoutConstants.PaymentStepKey });

        var controller = CreateController(engine.Object);

        // Act
        var result = await controller.Pay("token-1");

        // Assert
        var redirect = Assert.IsType<RedirectToRouteResult>(result);

        Assert.Equal(CheckoutConstants.RouteNames.Step, redirect.RouteName);
        Assert.NotNull(started);
        Assert.Equal(TransactionsConstants.ReferenceTypes.Transaction, started.ReferenceType);
        Assert.Equal("transaction-1", started.ReferenceId);
        Assert.Equal("/transactions/pay/token-1", started.ReturnUrl);
        Assert.True(CheckoutReturnInfo.IsLocalUrl(started.ReturnUrl));
        Assert.False(string.IsNullOrEmpty(started.ReturnText));
        Assert.False(string.IsNullOrEmpty(started.Title));
    }

    private static TransactionPayController CreateController(ICheckoutEngine engine)
    {
        var payLinks = new Mock<ITransactionPayLinkService>();
        payLinks.Setup(service => service.GetTransactionId("token-1")).Returns("transaction-1");

        var store = new FakeTransactionStore(new Transaction
        {
            ItemId = "transaction-1",
            Title = "Registration fee",
            Currency = "USD",
            Amount = 50m,
            TotalAmount = 50m,
            Status = TransactionStatus.Outstanding,
        });

        var services = new ServiceCollection();
        services.AddSingleton(engine);

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper
            .Setup(helper => helper.RouteUrl(It.Is<UrlRouteContext>(context => context.RouteName == TransactionsConstants.RouteNames.Pay)))
            .Returns("/transactions/pay/token-1");

        return new TransactionPayController(
            payLinks.Object,
            TransactionManagerFactory.Create(store),
            new ModelShapeFactory(),
            new PassThroughStringLocalizer<TransactionPayController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services.BuildServiceProvider(),
                },
            },
            Url = urlHelper.Object,
        };
    }
}
