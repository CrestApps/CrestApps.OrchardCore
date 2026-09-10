using CrestApps.OrchardCore.PhoneNumbers.Core.Services;

namespace CrestApps.OrchardCore.Tests.Core.Services;

/// <summary>
/// Showing a phone number to a person.
/// </summary>
/// <remarks>
/// E.164 is what the platform stores and dials with, and it is the wrong thing to put in front of an agent:
/// "+17789012046" is a string to decode, not a number to read back to a customer mid-call.
/// </remarks>
public sealed class PhoneNumberDisplayFormatTests
{
    private readonly DefaultPhoneNumberService _service = new();

    [Fact]
    public void AStoredNumber_IsGroupedForReading()
    {
        // Act
        var formatted = _service.FormatForDisplay("+17789012046");

        // Assert
        Assert.Equal("+1 778-901-2046", formatted);
    }

    [Fact]
    public void ANumberInTheReadersOwnCountry_DropsTheCountryCode()
    {
        // Arrange
        // Nobody writes their own country's code on a local number, so an agent working one region reads it the
        // way they would dial it.

        // Act
        var formatted = _service.FormatForDisplay("+17789012046", "CA");

        // Assert
        Assert.Equal("(778) 901-2046", formatted);
    }

    [Fact]
    public void ANumberFromAnotherCountry_KeepsTheCountryCode()
    {
        // Arrange
        // Dropping it here would produce something that cannot be dialled from where the agent is sitting.

        // Act
        var formatted = _service.FormatForDisplay("+442071838750", "CA");

        // Assert
        Assert.StartsWith("+44", formatted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not a phone number")]
    [InlineData("12345")]
    [InlineData("+1")]
    public void SomethingThatIsNotAValidNumber_IsShownExactlyAsItWasStored(string stored)
    {
        // Assert
        // A half-parsed number shown as if it were real is worse than the raw value: the agent would read out
        // digits the customer never gave.
        Assert.Equal(stored, _service.FormatForDisplay(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingStored_StaysNothing(string stored)
    {
        // Assert
        Assert.Equal(stored, _service.FormatForDisplay(stored));
    }
}
