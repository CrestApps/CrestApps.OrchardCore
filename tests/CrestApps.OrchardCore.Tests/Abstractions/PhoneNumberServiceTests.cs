using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;

namespace CrestApps.OrchardCore.Tests.Abstractions;

public sealed class PhoneNumberServiceTests
{
    private readonly DefaultPhoneNumberService _service = new();

    [Fact]
    public void TryFormatToE164_WhenRegionProvided_ShouldFormatNationalNumber()
    {
        // Arrange & Act
        var result = _service.TryFormatToE164("702-499-3350", "US", out var e164);

        // Assert
        Assert.True(result);
        Assert.Equal("+17024993350", e164);
    }

    [Fact]
    public void TryFormatToE164_WhenInternationalFormat_ShouldNormalize()
    {
        // Arrange & Act
        var result = _service.TryFormatToE164("+1 (702) 499-3350", null, out var e164);

        // Assert
        Assert.True(result);
        Assert.Equal("+17024993350", e164);
    }

    [Fact]
    public void TryFormatToE164_WhenNoRegionAndNationalNumber_ShouldFail()
    {
        // Arrange & Act
        var result = _service.TryFormatToE164("7024993350", null, out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryFormatToE164_WhenItalianNumber_ShouldFormatCorrectly()
    {
        // Arrange & Act
        var result = _service.TryFormatToE164("06 6982 0001", "IT", out var e164);

        // Assert
        Assert.True(result);
        Assert.Equal("+390669820001", e164);
    }

    [Fact]
    public void TryFormatToE164_DistinguishesBetweenCountries()
    {
        // Arrange & Act
        _service.TryFormatToE164("2024561111", "US", out var usE164);
        _service.TryFormatToE164("2024561111", "GB", out var gbE164);

        // Assert - same digits, different countries produce different E.164
        Assert.Equal("+12024561111", usE164);
        Assert.NotEqual(usE164, gbE164);
    }

    [Fact]
    public void TryFormatToE164_WhenInvalidNumber_ShouldFail()
    {
        // Arrange & Act
        var result = _service.TryFormatToE164("123", "US", out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryFormatToE164_WhenNullOrEmpty_ShouldFail()
    {
        Assert.False(_service.TryFormatToE164(null, "US", out _));
        Assert.False(_service.TryFormatToE164("", "US", out _));
        Assert.False(_service.TryFormatToE164("   ", "US", out _));
    }

    [Theory]
    [InlineData("US", 1)]
    [InlineData("CA", 1)]
    [InlineData("GB", 44)]
    [InlineData("IT", 39)]
    [InlineData("DE", 49)]
    [InlineData("JP", 81)]
    public void GetCountryCode_ShouldReturnCorrectCodes(string regionCode, int expected)
    {
        Assert.Equal(expected, _service.GetCountryCode(regionCode));
    }

    [Fact]
    public void GetCountryCode_WhenUnknownRegion_ShouldReturnZero()
    {
        Assert.Equal(0, _service.GetCountryCode("XX"));
        Assert.Equal(0, _service.GetCountryCode(null));
    }

    [Fact]
    public void GetRegionCode_ShouldDetectRegionFromE164()
    {
        Assert.Equal("US", _service.GetRegionCode("+17024993350"));
        Assert.Equal("GG", _service.GetRegionCode("+447911123456"));
        Assert.Equal("IT", _service.GetRegionCode("+393312345678"));
    }

    [Fact]
    public void GetRegionCode_WhenInvalid_ShouldReturnNull()
    {
        Assert.Null(_service.GetRegionCode(null));
        Assert.Null(_service.GetRegionCode(""));
    }

    [Fact]
    public void IsValidNumber_ShouldValidateCorrectly()
    {
        Assert.True(_service.IsValidNumber("+17024993350", null));
        Assert.True(_service.IsValidNumber("7024993350", "US"));
        Assert.False(_service.IsValidNumber("123", "US"));
        Assert.False(_service.IsValidNumber(null, "US"));
    }

    [Fact]
    public void GetTimeZones_ShouldReturnTimeZonesForValidNumber()
    {
        // Arrange & Act
        var timeZones = _service.GetTimeZones("+17024993350");

        // Assert
        Assert.NotEmpty(timeZones);
    }

    [Fact]
    public void GetTimeZones_WhenInvalid_ShouldReturnEmpty()
    {
        Assert.Empty(_service.GetTimeZones(null));
        Assert.Empty(_service.GetTimeZones(""));
    }

    [Fact]
    public void GetSupportedRegions_ShouldReturnNonEmptyCollection()
    {
        var regions = _service.GetSupportedRegions();

        Assert.NotEmpty(regions);
        Assert.Contains("US", regions);
        Assert.Contains("GB", regions);
        Assert.Contains("IT", regions);
    }
}
