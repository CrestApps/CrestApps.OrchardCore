using System.Security.Claims;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Controllers;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using Xunit;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Refunding is the one action in the suite that moves money back out, so these tests pin the two things
/// that must never go wrong: only an authorized operator can reach the form, and the amount can never exceed
/// what is still refundable.
/// </summary>
public sealed class PaymentsAdminControllerTests
{
    [Fact]
    public async Task Index_WithoutTheRefundPermission_IsForbidden()
    {
        // Arrange
        var controller = CreateController(new InMemoryPaymentAttemptStore(), new InMemoryPaymentRefundStore(), Mock.Of<ICheckoutRefundService>(), authorized: false);

        // Act
        var result = await controller.Index(new PaymentsAdminIndexOptions(), new PagerParameters(), null, null);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Refund_WithoutTheRefundPermission_IsForbidden()
    {
        // Arrange
        var attemptStore = new InMemoryPaymentAttemptStore(CreateSettledAttempt());
        var controller = CreateController(attemptStore, new InMemoryPaymentRefundStore(), Mock.Of<ICheckoutRefundService>(), authorized: false);

        // Act
        var result = await controller.Refund("attempt-1");

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// An attempt that never settled has no gateway transaction to refund, so offering the form would only
    /// produce a refund that can fail.
    /// </summary>
    [Fact]
    public async Task Refund_ForAnUnsettledAttempt_IsNotFound()
    {
        // Arrange
        var attempt = CreateSettledAttempt();

        attempt.State = PaymentAttemptState.Pending;

        var controller = CreateController(new InMemoryPaymentAttemptStore(attempt), new InMemoryPaymentRefundStore(), Mock.Of<ICheckoutRefundService>(), authorized: true);

        // Act
        var result = await controller.Refund("attempt-1");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Refund_ShowsTheRemainingRefundableAmount()
    {
        // Arrange
        var refundStore = new InMemoryPaymentRefundStore();

        await refundStore.CreateAsync(new PaymentRefund
        {
            ItemId = "refund-1",
            OriginalTransactionId = "pi_1",
            Currency = "USD",
            RefundGrossAmount = 30m,
            Status = RefundStatus.Succeeded,
        }, TestContext.Current.CancellationToken);

        var controller = CreateController(new InMemoryPaymentAttemptStore(CreateSettledAttempt()), refundStore, Mock.Of<ICheckoutRefundService>(), authorized: true);

        // Act
        var result = await controller.Refund("attempt-1");

        // Assert
        var model = Assert.IsType<RefundRequestViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(30m, model.AlreadyRefunded);
        Assert.Equal(78m, model.Amount);
    }

    /// <summary>
    /// Over-refunding is the failure that costs real money, so the form refuses it before the refund service
    /// is ever called.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(200)]
    public async Task RefundPost_WithAnInvalidAmount_DoesNotCallTheRefundService(decimal amount)
    {
        // Arrange
        var refundService = new Mock<ICheckoutRefundService>();
        var controller = CreateController(new InMemoryPaymentAttemptStore(CreateSettledAttempt()), new InMemoryPaymentRefundStore(), refundService.Object, authorized: true);

        // Act
        var result = await controller.RefundPost("attempt-1", new RefundRequestViewModel { Amount = amount });

        // Assert
        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);

        refundService.Verify(
            service => service.RequestRefundAsync(It.IsAny<RequestPaymentRefundContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundPost_WithAValidAmount_RefundsAgainstTheOriginalTransaction()
    {
        // Arrange
        RequestPaymentRefundContext captured = null;

        var refundService = new Mock<ICheckoutRefundService>();
        refundService
            .Setup(service => service.RequestRefundAsync(It.IsAny<RequestPaymentRefundContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RequestPaymentRefundContext context, CancellationToken _) =>
            {
                captured = context;

                return new PaymentRefund { ItemId = "refund-1", Status = RefundStatus.Succeeded };
            });

        var controller = CreateController(new InMemoryPaymentAttemptStore(CreateSettledAttempt()), new InMemoryPaymentRefundStore(), refundService.Object, authorized: true);

        // Act
        var result = await controller.RefundPost("attempt-1", new RefundRequestViewModel { Amount = 50m, Reason = "Customer changed their mind." });

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(captured);
        Assert.Equal("pi_1", captured.OriginalTransactionId);
        Assert.Equal("session-1", captured.SessionId);
        Assert.Equal(50m, captured.Amount);
        Assert.Equal("Customer changed their mind.", captured.Reason);
    }

    private static PaymentAttempt CreateSettledAttempt()
        => new()
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = "Stripe",
            ObligationId = "one-time",
            State = PaymentAttemptState.Succeeded,
            TransactionId = "pi_1",
            Currency = "USD",
            ConfirmedAmount = 100m,
            ConfirmedTaxAmount = 8m,
        };

    private static PaymentsAdminController CreateController(
        InMemoryPaymentAttemptStore attemptStore,
        InMemoryPaymentRefundStore refundStore,
        ICheckoutRefundService refundService,
        bool authorized)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var controller = new PaymentsAdminController(
            attemptStore,
            refundStore,
            refundService,
            [],
            authorizationService.Object,
            Mock.Of<INotifier>(),
            new PassThroughHtmlLocalizer<PaymentsAdminController>(),
            new PassThroughStringLocalizer<PaymentsAdminController>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return controller;
    }
}
