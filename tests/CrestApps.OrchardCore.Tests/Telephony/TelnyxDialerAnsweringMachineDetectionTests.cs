using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// How the dialer's answering-machine screening is asked of Telnyx. Without it, a voicemail that picks up is connected
/// to the agent like a person.
/// </summary>
public sealed class TelnyxDialerAnsweringMachineDetectionTests
{
    [Theory]
    [InlineData("standard", "detect")]
    [InlineData("Standard ", "detect")]
    [InlineData("premium", "premium")]
    [InlineData("disabled", null)]
    [InlineData("", null)]
    public void AnsweringMachineDetectionMode_MapsTheDialersRequestToTelnyxs(string requested, string expected)
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            [TelephonyConstants.RequestMetadata.AnsweringMachineDetection] = requested,
        };

        // Act
        var mode = TelnyxTelephonyProvider.AnsweringMachineDetectionMode(metadata);

        // Assert
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void AnsweringMachineDetectionMode_WhenNothingWasRequested_DoesNotScreen()
    {
        // Act
        var mode = TelnyxTelephonyProvider.AnsweringMachineDetectionMode(new Dictionary<string, string>());

        // Assert
        Assert.Null(mode);
    }
}
