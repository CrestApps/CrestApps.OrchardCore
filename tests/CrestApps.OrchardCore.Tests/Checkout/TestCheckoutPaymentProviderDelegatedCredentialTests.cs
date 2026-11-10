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

    [Fact]
    public async Task BeginAsync_WithThe3dsCredentialAndNoResult_ReportsTheCardBrand()
    {
        // Arrange
        var provider = TestCheckoutPaymentProviderTests.CreateProvider();

        // Act
        var result = await provider.BeginAsync(Context(TestCheckoutPaymentProvider.AuthenticationRequiredCredential), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TestCheckoutPaymentProvider.TestCardBrand, result.CardBrand);
        Assert.True(provider.Capabilities.AcceptsExternalAuthentication);
    }

    [Theory]
    [InlineData(ThreeDSecureOutcomes.Authenticated)]
    [InlineData(ThreeDSecureOutcomes.AttemptAcknowledged)]
    public async Task BeginAsync_WithThe3dsCredentialAndAnAuthenticatedResult_Succeeds(string outcome)
    {
        // Arrange
        var provider = TestCheckoutPaymentProviderTests.CreateProvider();
        var authentication = ThreeDSecureResultTests.Authenticated();

        authentication.Outcome = outcome;

        // Act
        var result = await provider.BeginAsync(Context(TestCheckoutPaymentProvider.AuthenticationRequiredCredential, authentication: authentication), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresAction);
        Assert.False(result.Declined);
    }

    [Theory]
    [InlineData(ThreeDSecureOutcomes.Denied)]
    [InlineData(ThreeDSecureOutcomes.Rejected)]
    public async Task BeginAsync_WithThe3dsCredentialAndARefusedResult_Declines(string outcome)
    {
        // Arrange
        var provider = TestCheckoutPaymentProviderTests.CreateProvider();

        // Act
        var result = await provider.BeginAsync(
            Context(TestCheckoutPaymentProvider.AuthenticationRequiredCredential, authentication: new ThreeDSecureResult { Outcome = outcome }),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.Declined);
        Assert.False(result.RequiresAuthentication);
    }

    [Fact]
    public async Task BeginAsync_WithThe3dsCredentialAndAnIncompleteResult_Fails()
    {
        // Arrange
        var provider = TestCheckoutPaymentProviderTests.CreateProvider();
        var authentication = ThreeDSecureResultTests.Authenticated();

        authentication.Cryptogram = "short";

        // Act
        var result = await provider.BeginAsync(Context(TestCheckoutPaymentProvider.AuthenticationRequiredCredential, authentication: authentication), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.Declined);
    }

    private static BeginPaymentContext Context(string credential, string credentialType = DelegatedCredentialTypes.Test, ThreeDSecureResult authentication = null)
    {
        var context = CreateContext(credential, credentialType);

        if (authentication is not null)
        {
            var providerData = new Dictionary<string, string>(context.ProviderData);

            authentication.CopyTo(providerData);
            context.ProviderData = providerData;
        }

        return context;
    }

    private static BeginPaymentContext CreateContext(string credential, string credentialType)
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
