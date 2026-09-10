using System.Net;
using CrestApps.OrchardCore.Asterisk.Services;
using Polly.Timeout;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskAriOutcomeClassifierTests
{
    [Fact]
    public void IsProvisioningOutcomeAmbiguous_WhenAriExceptionHasNullStatus_ReturnsTrue()
    {
        // Arrange
        var exception = new AsteriskAriException("createExternalMedia", null, "The client never reached Asterisk.");

        // Act
        var result = AsteriskAriOutcomeClassifier.IsProvisioningOutcomeAmbiguous(exception);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void IsProvisioningOutcomeAmbiguous_WhenAriExceptionHasServerErrorStatus_ReturnsTrue(HttpStatusCode statusCode)
    {
        // Arrange
        var exception = new AsteriskAriException("createExternalMedia", statusCode, "Asterisk returned a server error.");

        // Act
        var result = AsteriskAriOutcomeClassifier.IsProvisioningOutcomeAmbiguous(exception);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public void IsProvisioningOutcomeAmbiguous_WhenAriExceptionHasClientRejectionStatus_ReturnsFalse(HttpStatusCode statusCode)
    {
        // Arrange
        var exception = new AsteriskAriException("createExternalMedia", statusCode, "Asterisk rejected the request.");

        // Act
        var result = AsteriskAriOutcomeClassifier.IsProvisioningOutcomeAmbiguous(exception);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsProvisioningOutcomeAmbiguous_WhenOperationCanceled_ReturnsTrue()
    {
        // Arrange
        var exception = new OperationCanceledException();

        // Act
        var result = AsteriskAriOutcomeClassifier.IsProvisioningOutcomeAmbiguous(exception);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsProvisioningOutcomeAmbiguous_WhenResilienceTimeoutRejected_ReturnsTrue()
    {
        // Arrange
        // The standard resilience pipeline surfaces an exhausted attempt or total-request timeout as a Polly
        // TimeoutRejectedException, which does not derive from OperationCanceledException. The classifier must still
        // treat it as ambiguous so the durable provisioning record is retained for the age-gated reconciler.
        var exception = new TimeoutRejectedException("The ARI request timed out.");

        // Act
        var result = AsteriskAriOutcomeClassifier.IsProvisioningOutcomeAmbiguous(exception);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsProvisioningOutcomeAmbiguous_WhenGenericException_ReturnsFalse()
    {
        // Arrange
        var exception = new InvalidOperationException("A post-provisioning failure after ARI returned normally.");

        // Act
        var result = AsteriskAriOutcomeClassifier.IsProvisioningOutcomeAmbiguous(exception);

        // Assert
        Assert.False(result);
    }
}
