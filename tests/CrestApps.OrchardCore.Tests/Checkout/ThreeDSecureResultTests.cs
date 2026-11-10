using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.Endpoints;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A 3-D Secure result run outside the provider is checked before any provider sees it: a result that lets the payment
/// go ahead carries a 28-character base64 cryptogram, a known ECI, a transaction id and a version; it travels in
/// server-only provider data a browser can't set.
/// </summary>
public sealed class ThreeDSecureResultTests
{
    private const string Cryptogram = "M6+990I6FLD8Y6rZz9d5QbfrMNY=";

    [Fact]
    public void GetInvalidMember_WithACompleteAuthenticatedResult_IsValid()
    {
        // Act & Assert
        Assert.Null(Authenticated().GetInvalidMember());
        Assert.True(Authenticated().AllowsPayment);
    }

    [Theory]
    [InlineData("M6+990I6FLD8Y6rZz9d5QbfrMN=")]
    [InlineData("M6+990I6FLD8Y6rZz9d5QbfrMNYA")]
    [InlineData("M6+990I6FLD8Y6rZz9d5QbfrMNY")]
    [InlineData("not base64 at all, 28 chars!")]
    [InlineData("")]
    [InlineData(null)]
    public void GetInvalidMember_WithACryptogramThatIsNot20BytesOfBase64_NamesTheCryptogram(string cryptogram)
    {
        // Arrange
        var result = Authenticated();

        result.Cryptogram = cryptogram;

        // Act & Assert
        Assert.Equal(nameof(ThreeDSecureResult.Cryptogram), result.GetInvalidMember());
    }

    [Theory]
    [InlineData("00")]
    [InlineData("03")]
    [InlineData("5")]
    [InlineData(null)]
    public void GetInvalidMember_WithAnUnknownEci_NamesTheIndicator(string eci)
    {
        // Arrange
        var result = Authenticated();

        result.ElectronicCommerceIndicator = eci;

        // Act & Assert
        Assert.Equal(nameof(ThreeDSecureResult.ElectronicCommerceIndicator), result.GetInvalidMember());
    }

    [Theory]
    [InlineData("01")]
    [InlineData("02")]
    [InlineData("05")]
    [InlineData("06")]
    [InlineData("07")]
    public void GetInvalidMember_WithEachKnownEci_IsValid(string eci)
    {
        // Arrange
        var result = Authenticated();

        result.ElectronicCommerceIndicator = eci;

        // Act & Assert
        Assert.Null(result.GetInvalidMember());
    }

    [Theory]
    [InlineData("2.2")]
    [InlineData("v2.2.0")]
    [InlineData("")]
    public void GetInvalidMember_WithAMalformedVersion_NamesTheVersion(string version)
    {
        // Arrange
        var result = Authenticated();

        result.Version = version;

        // Act & Assert
        Assert.Equal(nameof(ThreeDSecureResult.Version), result.GetInvalidMember());
    }

    [Fact]
    public void GetInvalidMember_WithoutATransactionId_NamesTheTransactionId()
    {
        // Arrange
        var result = Authenticated();

        result.TransactionId = " ";

        // Act & Assert
        Assert.Equal(nameof(ThreeDSecureResult.TransactionId), result.GetInvalidMember());
    }

    [Fact]
    public void GetInvalidMember_WithAnUnknownOutcome_NamesTheOutcome()
    {
        // Act & Assert
        Assert.Equal(nameof(ThreeDSecureResult.Outcome), new ThreeDSecureResult { Outcome = "maybe" }.GetInvalidMember());
        Assert.Equal(nameof(ThreeDSecureResult.Outcome), new ThreeDSecureResult().GetInvalidMember());
    }

    [Theory]
    [InlineData(ThreeDSecureOutcomes.Denied)]
    [InlineData(ThreeDSecureOutcomes.Rejected)]
    [InlineData(ThreeDSecureOutcomes.Abandoned)]
    public void GetInvalidMember_WithAnOutcomeThatStopsThePayment_NeedsNoDetails(string outcome)
    {
        // Arrange
        var result = new ThreeDSecureResult { Outcome = outcome };

        // Act & Assert
        Assert.Null(result.GetInvalidMember());
        Assert.False(result.AllowsPayment);
    }

    [Theory]
    [InlineData(ThreeDSecureOutcomes.AttemptAcknowledged)]
    [InlineData(ThreeDSecureOutcomes.Informational)]
    public void GetInvalidMember_WithAnAttemptOrInformationalOutcome_NeedsTheDetails(string outcome)
    {
        // Arrange
        var result = new ThreeDSecureResult { Outcome = outcome };

        // Act & Assert
        Assert.True(result.AllowsPayment);
        Assert.Equal(nameof(ThreeDSecureResult.Cryptogram), result.GetInvalidMember());
    }

    [Fact]
    public void CopyTo_ThenFrom_RoundTripsTheResult()
    {
        // Arrange
        var providerData = new Dictionary<string, string>();
        var result = Authenticated();

        result.TransactionStatus = "Y";

        // Act
        result.CopyTo(providerData);
        var read = ThreeDSecureResult.From(providerData);

        // Assert
        Assert.Equal(result.Outcome, read.Outcome);
        Assert.Equal(result.Cryptogram, read.Cryptogram);
        Assert.Equal(result.ElectronicCommerceIndicator, read.ElectronicCommerceIndicator);
        Assert.Equal(result.TransactionId, read.TransactionId);
        Assert.Equal(result.Version, read.Version);
        Assert.Equal("Y", read.TransactionStatus);
    }

    [Fact]
    public void From_WithoutAnOutcome_IsNull()
    {
        // Act & Assert
        Assert.Null(ThreeDSecureResult.From(new Dictionary<string, string> { [CheckoutPaymentDataKeys.ThreeDSecureCryptogram] = Cryptogram }));
        Assert.Null(ThreeDSecureResult.From(null));
    }

    [Fact]
    public void SanitizeProviderData_DropsThe3dsKeysABrowserSends()
    {
        // Arrange
        var providerData = new Dictionary<string, string> { ["paymentMethodId"] = "pm_1" };

        Authenticated().CopyTo(providerData);

        // Act
        var sanitized = PaymentEndpoints.SanitizeProviderData(providerData);

        // Assert
        Assert.Equal(["paymentMethodId"], sanitized.Keys);
        Assert.True(CheckoutPaymentDataKeys.IsServerOnly(CheckoutPaymentDataKeys.ThreeDSecureCryptogram));
        Assert.True(CheckoutPaymentDataKeys.IsServerOnly(CheckoutPaymentDataKeys.DelegatedCredential));
        Assert.False(CheckoutPaymentDataKeys.IsServerOnly("paymentMethodId"));
    }

    internal static ThreeDSecureResult Authenticated()
        => new()
        {
            Outcome = ThreeDSecureOutcomes.Authenticated,
            Cryptogram = Cryptogram,
            ElectronicCommerceIndicator = "05",
            TransactionId = "f879ea1c-aa2c-4441-806d-e30406466d79",
            Version = "2.2.0",
        };
}
