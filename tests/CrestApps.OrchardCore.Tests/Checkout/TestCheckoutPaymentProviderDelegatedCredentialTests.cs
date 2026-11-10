using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// An agent pays the test provider with a delegated test credential whose value chooses the outcome, so agent
/// checkouts reach the paid, declined and authentication paths without a gateway; a real credential is refused.
/// </summary>
public sealed class TestCheckoutPaymentProviderDelegatedCredentialTests
{
    [Fact]
    public async Task BeginAsync_WithTheSuccessCredential_Succeeds()
    {
        // Arrange
        var provider = TestCheckoutPaymentProviderTests.CreateProvider();

        // Act
        var result = await provider.BeginAsync(Context(TestCheckoutPaymentProvider.SuccessCredential), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.Declined);
        Assert.StartsWith("test_", result.ProviderReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BeginAsync_WithTheDeclineCredential_Declines()
    {
        // Arrange
        var provider = TestCheckoutPaymentProviderTests.CreateProvider();

        // Act
        var result = await provider.BeginAsync(Context(TestCheckoutPaymentProvider.DeclinedCredential), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.Declined);
        Assert.False(result.RequiresAuthentication);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public async Task BeginAsync_WithThe3dsCredential_AsksForAuthentication()
    {
        // Arrange
        var provider = TestCheckoutPaymentProviderTests.CreateProvider();

        // Act
        var result = await provider.BeginAsync(Context(TestCheckoutPaymentProvider.AuthenticationRequiredCredential), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.RequiresAuthentication);
        Assert.False(string.IsNullOrEmpty(result.ProviderReference));
    }

    [Fact]
    public async Task BeginAsync_WithAnUnknownTestCredential_Declines()
    {
        // Arrange
        var provider = TestCheckoutPaymentProviderTests.CreateProvider();

        // Act
        var result = await provider.BeginAsync(Context("tok_test_unknown"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.Declined);
    }

    [Fact]
    public async Task BeginAsync_WithACredentialThatIsNotATestOne_Fails()
    {
        // Arrange
        var provider = TestCheckoutPaymentProviderTests.CreateProvider();

        // Act
        var result = await provider.BeginAsync(
            Context(TestCheckoutPaymentProvider.SuccessCredential, DelegatedCredentialTypes.StripeSharedPaymentToken),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.Declined);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    private static BeginPaymentContext Context(string credential, string credentialType = DelegatedCredentialTypes.Test)
        => new()
        {
            Attempt = new PaymentAttempt
            {
                ItemId = "attempt-1",
                SessionId = "session-1",
                ProviderKey = TestCheckoutPaymentProvider.ProviderKey,
                ObligationId = CheckoutObligations.OneTime,
                ExpectedAmount = 30m,
                Currency = "USD",
                State = PaymentAttemptState.Created,
                IdempotencyKey = "checkout_session-1_" + CheckoutObligations.OneTime,
            },
            ProviderData = new Dictionary<string, string>
            {
                [CheckoutPaymentDataKeys.DelegatedCredential] = credential,
                [CheckoutPaymentDataKeys.DelegatedCredentialType] = credentialType,
            },
        };
}
