using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Every voice provider — and the router in front of them — carried its own private copy of "build a failure
/// result", identical apart from the provider name. A failure that loses its provider name is a failure the
/// operator cannot attribute to anything, which is exactly when they need to.
/// </summary>
public sealed class ContactCenterVoiceProviderResultTests
{
    [Fact]
    public void AFailure_CarriesTheCodeMessageAndTheProviderThatProducedIt()
    {
        // Act
        var result = ContactCenterVoiceProviderResult.Failure("Telnyx", "not_found", "The call no longer exists.");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("not_found", result.ErrorCode);
        Assert.Equal("The call no longer exists.", result.ErrorMessage);
        Assert.Equal("Telnyx", result.ProviderName);
    }

    [Fact]
    public void AFailure_IsNotAnUnknownOutcome()
    {
        // A failure and "we could not tell what happened" lead to different recovery: one is safe to retry, the
        // other is the case where retrying can do the thing twice.
        var result = ContactCenterVoiceProviderResult.Failure("Telnyx", "refused", "The provider refused.");

        Assert.False(result.OutcomeUnknown);
    }
}
