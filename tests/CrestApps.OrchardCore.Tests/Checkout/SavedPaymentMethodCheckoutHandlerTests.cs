using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A checkout that keeps the card adds it to the buyer's kept payment methods when it completes, so a later charge can
/// find it without knowing which checkout kept it.
/// </summary>
public sealed class SavedPaymentMethodCheckoutHandlerTests
{
    [Fact]
    public async Task CompletedAsync_ForACheckoutThatKeptTheCard_KeepsItForTheOwner()
    {
        // Arrange
        var (handler, store) = CreateHandler(CreateAttempt("test_save_1", PaymentAttemptState.Succeeded));

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(CreateSession(savesCard: true, ownerId: "user-1"))));

        // Assert
        store.Verify(
            saved => saved.SaveAsync(
                "user-1",
                It.Is<SavedPaymentMethod>(method => method.ProviderKey == TestCheckoutPaymentProvider.ProviderKey && method.PaymentMethodReference == "test_pm_attempt-1"),
                "session-1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompletedAsync_ForAnOrdinaryCheckout_KeepsNothing()
    {
        // Arrange
        var (handler, store) = CreateHandler(CreateAttempt("test_save_1", PaymentAttemptState.Succeeded));

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(CreateSession(savesCard: false, ownerId: "user-1"))));

        // Assert
        VerifyNothingKept(store);
    }

    [Fact]
    public async Task CompletedAsync_ForAGuestCheckout_KeepsNothing()
    {
        // Arrange
        var (handler, store) = CreateHandler(CreateAttempt("test_save_1", PaymentAttemptState.Succeeded));

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(CreateSession(savesCard: true, ownerId: null))));

        // Assert
        VerifyNothingKept(store);
    }

    [Fact]
    public async Task CompletedAsync_ForAPaymentThatFailed_KeepsNothing()
    {
        // Arrange
        var (handler, store) = CreateHandler(CreateAttempt("test_save_1", PaymentAttemptState.Failed));

        // Act
        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(CreateSession(savesCard: true, ownerId: "user-1"))));

        // Assert
        VerifyNothingKept(store);
    }

    [Fact]
    public async Task CompletedAsync_WhenTheStoreFails_DoesNotFailTheCheckout()
    {
        // Arrange
        var (handler, store) = CreateHandler(CreateAttempt("test_save_1", PaymentAttemptState.Succeeded));

        store
            .Setup(saved => saved.SaveAsync(It.IsAny<string>(), It.IsAny<SavedPaymentMethod>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The database is unavailable."));

        // Act
        var exception = await Record.ExceptionAsync(() => handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(CreateSession(savesCard: true, ownerId: "user-1")))));

        // Assert
        Assert.Null(exception);
    }

    private static void VerifyNothingKept(Mock<ISavedPaymentMethodStore> store)
        => store.Verify(
            saved => saved.SaveAsync(It.IsAny<string>(), It.IsAny<SavedPaymentMethod>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private static (SavedPaymentMethodCheckoutHandler Handler, Mock<ISavedPaymentMethodStore> Store) CreateHandler(params PaymentAttempt[] attempts)
    {
        var store = new Mock<ISavedPaymentMethodStore>();

        var handler = new SavedPaymentMethodCheckoutHandler(
            store.Object,
            new InMemoryPaymentAttemptStore(attempts),
            [TestCheckoutPaymentProviderTests.CreateProvider()],
            NullLogger<SavedPaymentMethodCheckoutHandler>.Instance);

        return (handler, store);
    }

    private static CheckoutSession CreateSession(bool savesCard, string ownerId)
        => new()
        {
            SessionId = "session-1",
            OwnerId = ownerId,
            Status = CheckoutSessionStatus.Completed,
            Currency = "USD",
            SavePaymentMethod = savesCard,
        };

    private static PaymentAttempt CreateAttempt(string providerReference, PaymentAttemptState state)
        => new()
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = TestCheckoutPaymentProvider.ProviderKey,
            ProviderReference = providerReference,
            State = state,
            Currency = "USD",
            ExpectedAmount = 25m,
        };
}
