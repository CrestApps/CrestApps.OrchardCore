using CrestApps.OrchardCore.Banking.Core.Services;

namespace CrestApps.OrchardCore.Tests.Banking;

public sealed class UnitedStatesBankRoutingValidatorTests
{
    [Theory]
    [InlineData("021000021", "021000021")]
    [InlineData("011000015", "011000015")]
    [InlineData("111000025", "111000025")]
    [InlineData(" 0210-0002 1 ", "021000021")]
    public void TryNormalizeRoutingNumber_AValidAbaNumber_IsAcceptedWithoutSeparators(string value, string expected)
    {
        // Arrange
        var validator = new UnitedStatesBankRoutingValidator();

        // Act
        var accepted = validator.TryNormalizeRoutingNumber(value, out var normalized);

        // Assert
        Assert.True(accepted);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("021000022")]
    [InlineData("02100002")]
    [InlineData("0210000210")]
    [InlineData("000000000")]
    [InlineData("02100002A")]
    [InlineData("")]
    [InlineData(null)]
    public void TryNormalizeRoutingNumber_AWrongNumber_IsRefused(string value)
    {
        // Arrange
        var validator = new UnitedStatesBankRoutingValidator();

        // Act
        var accepted = validator.TryNormalizeRoutingNumber(value, out var normalized);

        // Assert
        Assert.False(accepted);
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData("1234", "1234")]
    [InlineData("1234-5678 90", "1234567890")]
    [InlineData("12345678901234567", "12345678901234567")]
    [InlineData("ab12cd34", "AB12CD34")]
    public void TryNormalizeAccountNumber_UpTo17LettersAndDigits_IsAccepted(string value, string expected)
    {
        // Arrange
        var validator = new UnitedStatesBankRoutingValidator();

        // Act
        var accepted = validator.TryNormalizeAccountNumber(value, out var normalized);

        // Assert
        Assert.True(accepted);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012345678")]
    [InlineData("1234#5678")]
    public void TryNormalizeAccountNumber_ANumberAnAchEntryCannotHold_IsRefused(string value)
    {
        // Arrange
        var validator = new UnitedStatesBankRoutingValidator();

        // Act
        var accepted = validator.TryNormalizeAccountNumber(value, out _);

        // Assert
        Assert.False(accepted);
    }
}
