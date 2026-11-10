using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Checkout;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// Delegated 3-D Secure is only offered when the store gave its acquirer details and the agent said it runs 3-D Secure;
/// a checkout waiting for the agent's result refuses a complete without one, cancels on a refused outcome, and pays on
/// an authenticated one.
/// </summary>
public sealed class DelegatedAuthenticationTests
{
    [Fact]
    public void IsConfigured_NeedsTheSwitchAndEveryRequiredAcquirerDetail()
    {
        // Arrange
        var settings = Settings();

        // Act & Assert
        Assert.True(DelegatedAuthentication.IsConfigured(settings));

        settings.EnableDelegatedAuthentication = false;
        Assert.False(DelegatedAuthentication.IsConfigured(settings));

        settings = Settings();
        settings.AcquirerCountry = "USA";
        Assert.False(DelegatedAuthentication.IsConfigured(settings));

        settings = Settings();
        settings.AcquirerMerchantId = " ";
        Assert.False(DelegatedAuthentication.IsConfigured(settings));

        settings = Settings();
        settings.AuthenticationMerchantName = null;
        Assert.False(DelegatedAuthentication.IsConfigured(settings));

        // The requestor ID is optional.
        settings = Settings();
        settings.ThreeDSecureRequestorId = null;
        Assert.True(DelegatedAuthentication.IsConfigured(settings));
    }

    [Fact]
    public void AgentRunsThreeDSecure_ReadsWhatTheAgentSaidWhenItMadeTheCheckout()
    {
        // Arrange
        var checkout = new AgentCheckout();

        // Act & Assert
        Assert.False(DelegatedAuthentication.AgentRunsThreeDSecure(checkout));

        checkout.SetProperty(DelegatedAuthentication.AgentRunsThreeDSecureProperty, "true");
        Assert.True(DelegatedAuthentication.AgentRunsThreeDSecure(checkout));
    }

    [Fact]
    public void BuildMetadata_CarriesTheAcquirerDetailsAndTheDirectoryServer()
    {
        // Act
        var metadata = DelegatedAuthentication.BuildMetadata(Settings(), AgentDirectoryServers.Mastercard);

        // Assert
        Assert.Equal("424242", metadata.AcquirerBin);
        Assert.Equal("US", metadata.AcquirerCountry);
        Assert.Equal("acq_merchant_1", metadata.AcquirerMerchantId);
        Assert.Equal("Example Store", metadata.MerchantName);
        Assert.Equal("req_1", metadata.RequestorId);
        Assert.Equal("mastercard", metadata.DirectoryServer);
    }

    [Theory]
    [InlineData("visa", AgentDirectoryServers.Visa)]
    [InlineData("Visa", AgentDirectoryServers.Visa)]
    [InlineData("mastercard", AgentDirectoryServers.Mastercard)]
    [InlineData("amex", AgentDirectoryServers.AmericanExpress)]
    [InlineData("american_express", AgentDirectoryServers.AmericanExpress)]
    [InlineData("discover", null)]
    [InlineData(null, null)]
    public void FromBrand_MapsACardBrandToItsDirectoryServer(string brand, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, AgentDirectoryServers.FromBrand(brand));
    }

    [Fact]
    public void Classify_WithoutAPendingAuthentication_PaysAsUsual()
    {
        // Act & Assert
        Assert.Equal(DelegatedAuthenticationStep.None, DelegatedAuthentication.Classify(null, ThreeDSecureResultTests.Authenticated()));
        Assert.Equal(DelegatedAuthenticationStep.None, DelegatedAuthentication.Classify(null, null));
    }

    [Fact]
    public void Classify_WhileWaitingAndWithoutAResult_IsMissing()
    {
        // Act & Assert
        Assert.Equal(DelegatedAuthenticationStep.Missing, DelegatedAuthentication.Classify(Pending(), null));
    }

    [Fact]
    public void Classify_WhileWaitingWithAMalformedResult_IsInvalid()
    {
        // Arrange
        var result = ThreeDSecureResultTests.Authenticated();

        result.Cryptogram = "AAAA";

        // Act & Assert
        Assert.Equal(DelegatedAuthenticationStep.Invalid, DelegatedAuthentication.Classify(Pending(), result));
    }

    [Theory]
    [InlineData(ThreeDSecureOutcomes.Denied)]
    [InlineData(ThreeDSecureOutcomes.Rejected)]
    [InlineData(ThreeDSecureOutcomes.Abandoned)]
    [InlineData(ThreeDSecureOutcomes.NotSupported)]
    public void Classify_WhileWaitingWithARefusedOutcome_IsNotAuthenticated(string outcome)
    {
        // Act & Assert
        Assert.Equal(DelegatedAuthenticationStep.NotAuthenticated, DelegatedAuthentication.Classify(Pending(), new ThreeDSecureResult { Outcome = outcome }));
    }

    [Fact]
    public void Classify_WhileWaitingWithAnAuthenticatedResult_Pays()
    {
        // Act & Assert
        Assert.Equal(DelegatedAuthenticationStep.Pay, DelegatedAuthentication.Classify(Pending(), ThreeDSecureResultTests.Authenticated()));
    }

    [Fact]
    public void SendsResult_OnlyForAValidAuthenticatedResultTheCheckoutAskedForOrTheStoreTakes()
    {
        // Arrange
        var result = ThreeDSecureResultTests.Authenticated();

        // Act & Assert
        Assert.True(DelegatedAuthentication.SendsResult(result, Pending(), usable: false));
        Assert.True(DelegatedAuthentication.SendsResult(result, pending: null, usable: true));
        Assert.False(DelegatedAuthentication.SendsResult(result, pending: null, usable: false));
        Assert.False(DelegatedAuthentication.SendsResult(null, Pending(), usable: true));
        Assert.False(DelegatedAuthentication.SendsResult(new ThreeDSecureResult { Outcome = ThreeDSecureOutcomes.Denied }, Pending(), usable: true));
    }

    private static AgentPendingAuthentication Pending()
        => new()
        {
            ProviderKey = "stripe",
            DirectoryServer = AgentDirectoryServers.Visa,
        };

    private static AgenticCommerceSettings Settings()
        => new()
        {
            EnableDelegatedAuthentication = true,
            AcquirerBin = "424242",
            AcquirerCountry = "us",
            AcquirerMerchantId = "acq_merchant_1",
            AuthenticationMerchantName = "Example Store",
            ThreeDSecureRequestorId = "req_1",
        };
}
