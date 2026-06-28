using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

public sealed class PhoneNumberVerificationProviderMappingTests
{
    private static readonly DateTime _verificationDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly IPhoneNumberService _phoneNumberService = new DefaultPhoneNumberService();

    [Fact]
    public void AbstractApi_MapsNestedFormatAndCountryObjects()
    {
        // Arrange
        // The live AbstractAPI Phone Validation response nests the formatted numbers under "format"
        // and the country details under "country".
        const string payload = """
        {
            "phone": "14152007986",
            "valid": true,
            "format": {
                "international": "+14152007986",
                "local": "(415) 200-7986"
            },
            "country": {
                "code": "US",
                "name": "United States",
                "prefix": "+1"
            },
            "location": "California",
            "type": "mobile",
            "carrier": "T-Mobile USA, Inc.",
            "phone_validation": {
                "line_status": "active",
                "minimum_age": "90 days"
            }
        }
        """;

        var parsed = JsonSerializer.Deserialize<AbstractApiResponse>(payload, PhoneNumberVerificationProviderJsonSerializerOptions.Default);

        // Act
        var result = AbstractApiPhoneNumberVerificationProvider.MapResponse(
            "14152007986",
            parsed,
            payload,
            _verificationDate,
            _phoneNumberService);

        // Assert
        Assert.Equal("+14152007986", result.NormalizedPhoneNumber);
        Assert.Equal("(415) 200-7986", result.NationalFormat);
        Assert.True(result.IsValid);
        Assert.True(result.IsReachable);
        Assert.True(result.IsMobile);
        Assert.Equal(PhoneNumberLineType.Mobile, result.LineType);
        Assert.Equal("US", result.CountryCode);
        Assert.Equal("United States", result.CountryName);
        Assert.Equal("+1", result.CountryPrefix);
        Assert.Equal("T-Mobile USA, Inc.", result.Carrier);
        Assert.Equal("active", result.LineStatus);
        Assert.Equal("90 days", result.MinimumAge);
        Assert.Equal(PhoneNumberVerificationStatus.Verified, result.Status);
        Assert.Equal(PhoneNumberVerificationsConstants.Providers.AbstractApi, result.VerificationProvider);
        Assert.Equal(_verificationDate, result.VerificationDateUtc);
        Assert.Equal("California", result.Metadata["location"]?.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(result.TimeZone));
    }

    [Theory]
    [InlineData("active", PhoneNumberVerificationStatus.Verified, true)]
    [InlineData("disconnected", PhoneNumberVerificationStatus.Invalid, false)]
    [InlineData("", PhoneNumberVerificationStatus.Verified, true)]
    public void AbstractApi_WhenLineStatusIsAvailable_RequiresActiveLine(
        string lineStatus,
        PhoneNumberVerificationStatus expectedStatus,
        bool expectedReachable)
    {
        // Arrange
        var payload = $$"""
        {
            "valid": true,
            "format": { "international": "+14152007986", "local": "(415) 200-7986" },
            "country": { "code": "US", "name": "United States", "prefix": "+1" },
            "phone_validation": {
                "line_status": "{{lineStatus}}"
            }
        }
        """;

        var parsed = JsonSerializer.Deserialize<AbstractApiResponse>(payload, PhoneNumberVerificationProviderJsonSerializerOptions.Default);

        // Act
        var result = AbstractApiPhoneNumberVerificationProvider.MapResponse(
            "14152007986",
            parsed,
            payload,
            _verificationDate,
            _phoneNumberService);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedReachable, result.IsReachable);
    }

    [Fact]
    public void AbstractApi_WhenInvalid_SetsInvalidStatus()
    {
        // Arrange
        const string payload = """
        {
            "phone": "1555",
            "valid": false,
            "format": { "international": "", "local": "" },
            "country": { "code": "", "name": "", "prefix": "" },
            "type": "",
            "carrier": ""
        }
        """;

        var parsed = JsonSerializer.Deserialize<AbstractApiResponse>(payload, PhoneNumberVerificationProviderJsonSerializerOptions.Default);

        // Act
        var result = AbstractApiPhoneNumberVerificationProvider.MapResponse(
            "1555",
            parsed,
            payload,
            _verificationDate,
            _phoneNumberService);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(PhoneNumberVerificationStatus.Invalid, result.Status);
        Assert.Equal(PhoneNumberLineType.Unknown, result.LineType);
    }

    [Fact]
    public void Twilio_MapsBasicFieldsAndDataPackages()
    {
        // Arrange
        const string payload = """
        {
            "calling_country_code": "1",
            "country_code": "US",
            "phone_number": "+14159929960",
            "national_format": "(415) 992-9960",
            "valid": true,
            "validation_errors": null,
            "line_status": { "status": "active", "error_code": null },
            "line_type_intelligence": {
                "type": "mobile",
                "carrier_name": "T-Mobile USA, Inc.",
                "mobile_country_code": "310",
                "mobile_network_code": "160",
                "error_code": null
            },
            "sms_pumping_risk": {
                "carrier_risk_category": "low",
                "number_blocked": false,
                "sms_pumping_risk_score": 10
            },
            "url": "https://lookups.twilio.com/v2/PhoneNumbers/+14159929960"
        }
        """;

        var parsed = JsonSerializer.Deserialize<TwilioLookupResponse>(payload, PhoneNumberVerificationProviderJsonSerializerOptions.Default);

        // Act
        var result = TwilioPhoneNumberVerificationProvider.MapResponse(
            "+14159929960",
            countryCode: null,
            parsed,
            payload,
            _verificationDate,
            _phoneNumberService);

        // Assert
        Assert.Equal("+14159929960", result.NormalizedPhoneNumber);
        Assert.Equal("(415) 992-9960", result.NationalFormat);
        Assert.True(result.IsValid);
        Assert.True(result.IsMobile);
        Assert.Equal(PhoneNumberLineType.Mobile, result.LineType);
        Assert.Equal("US", result.CountryCode);
        Assert.Equal("+1", result.CountryPrefix);
        Assert.Equal("T-Mobile USA, Inc.", result.Carrier);
        Assert.Equal("active", result.LineStatus);
        Assert.Equal(10, result.RiskScore);
        Assert.Equal("low", result.RiskLevel);
        Assert.False(result.IsAbuseDetected);
        Assert.Equal(PhoneNumberVerificationStatus.Verified, result.Status);
        Assert.Equal("310", result.Metadata["mobileCountryCode"]?.GetValue<string>());
        Assert.Equal("160", result.Metadata["mobileNetworkCode"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("active", PhoneNumberVerificationStatus.Verified, true)]
    [InlineData("disconnected", PhoneNumberVerificationStatus.Invalid, false)]
    [InlineData(null, PhoneNumberVerificationStatus.Verified, true)]
    public void Twilio_WhenLineStatusIsAvailable_RequiresActiveLine(
        string lineStatus,
        PhoneNumberVerificationStatus expectedStatus,
        bool expectedReachable)
    {
        // Arrange
        var lineStatusJson = lineStatus is null
            ? "null"
            : $$"""{ "status": "{{lineStatus}}", "error_code": null }""";
        var payload = $$"""
        {
            "phone_number": "+14159929960",
            "valid": true,
            "line_status": {{lineStatusJson}}
        }
        """;

        var parsed = JsonSerializer.Deserialize<TwilioLookupResponse>(payload, PhoneNumberVerificationProviderJsonSerializerOptions.Default);

        // Act
        var result = TwilioPhoneNumberVerificationProvider.MapResponse(
            "+14159929960",
            countryCode: null,
            parsed,
            payload,
            _verificationDate,
            _phoneNumberService);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedReachable, result.IsReachable);
    }

    [Fact]
    public void Veriphone_MapsSuccessResponse()
    {
        // Arrange
        const string payload = """
        {
            "status": "success",
            "phone": "+4915123577723",
            "phone_valid": true,
            "phone_type": "mobile",
            "phone_region": "Germany",
            "country": "Germany",
            "country_code": "DE",
            "country_prefix": "49",
            "international_number": "+49 1512 3577723",
            "local_number": "01512 3577723",
            "e164": "+4915123577723",
            "carrier": "T-Mobile"
        }
        """;

        var parsed = JsonSerializer.Deserialize<VeriphoneResponse>(payload, PhoneNumberVerificationProviderJsonSerializerOptions.Default);

        // Act
        var result = VeriphonePhoneNumberVerificationProvider.MapResponse(
            "+4915123577723",
            parsed,
            payload,
            _verificationDate,
            _phoneNumberService);

        // Assert
        Assert.Equal("+4915123577723", result.NormalizedPhoneNumber);
        Assert.Equal("01512 3577723", result.NationalFormat);
        Assert.True(result.IsValid);
        Assert.True(result.IsMobile);
        Assert.Equal(PhoneNumberLineType.Mobile, result.LineType);
        Assert.Equal("DE", result.CountryCode);
        Assert.Equal("Germany", result.CountryName);
        Assert.Equal("+49", result.CountryPrefix);
        Assert.Equal("Germany", result.Region);
        Assert.Equal("T-Mobile", result.Carrier);
        Assert.Equal(PhoneNumberVerificationStatus.Verified, result.Status);
        Assert.Equal("+49 1512 3577723", result.Metadata["internationalNumber"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("mobile", PhoneNumberLineType.Mobile)]
    [InlineData("landline", PhoneNumberLineType.Landline)]
    [InlineData("fixedVoip", PhoneNumberLineType.Voip)]
    [InlineData("nonFixedVoip", PhoneNumberLineType.Voip)]
    [InlineData("tollFree", PhoneNumberLineType.TollFree)]
    [InlineData("premium", PhoneNumberLineType.Premium)]
    [InlineData("unknown-value", PhoneNumberLineType.Unknown)]
    public void Twilio_MapsLineTypeIntelligenceVariants(string twilioType, PhoneNumberLineType expected)
    {
        // Arrange
        var payload = $$"""
        {
            "phone_number": "+14159929960",
            "valid": true,
            "line_type_intelligence": { "type": "{{twilioType}}" }
        }
        """;

        var parsed = JsonSerializer.Deserialize<TwilioLookupResponse>(payload, PhoneNumberVerificationProviderJsonSerializerOptions.Default);

        // Act
        var result = TwilioPhoneNumberVerificationProvider.MapResponse(
            "+14159929960",
            countryCode: null,
            parsed,
            payload,
            _verificationDate,
            _phoneNumberService);

        // Assert
        Assert.Equal(expected, result.LineType);
    }
}
