using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Migrations;

public sealed class ContactMethodMigrationsTests
{
    private readonly DefaultPhoneNumberService _phoneNumberService = new();

    [Theory]
    [InlineData("(403) 555-0101", "+14035550101", "CA", "4035550101")]
    [InlineData("4035550101", "+14035550101", "CA", "4035550101")]
    [InlineData("403-555-0101", "+14035550101", "CA", "4035550101")]
    [InlineData("+1 403 555 0101", "+14035550101", "CA", "4035550101")]
    [InlineData("17785550103", "+17785550103", "CA", "7785550103")]
    [InlineData("1 (778) 555-0103", "+17785550103", "CA", "7785550103")]
    [InlineData("+1 (778) 555-0103", "+17785550103", "CA", "7785550103")]
    [InlineData("(778) 555-0102", "+17785550102", "CA", "7785550102")]
    [InlineData("7785550102", "+17785550102", "CA", "7785550102")]
    public void TryMigratePhoneNumberField_WhenLegacyPhoneNumberUsesDifferentFormatting_StoresPhoneFieldProperties(
        string legacyPhoneNumber,
        string expectedPhoneNumber,
        string expectedCountryCode,
        string expectedNationalNumber)
    {
        // Arrange
        var innerContentItem = CreateLegacyPhoneNumberContentItem(legacyPhoneNumber);

        // Act
        var migrated = ContactMethodMigrations.TryMigratePhoneNumberField(innerContentItem, _phoneNumberService);

        // Assert
        Assert.True(migrated);

        var phoneNumberInfoPart = Assert.IsType<JsonObject>(innerContentItem[OmnichannelConstants.ContentParts.PhoneNumberInfo]);
        var numberNode = Assert.IsType<JsonObject>(phoneNumberInfoPart["Number"]);

        Assert.Null(numberNode["Text"]);
        Assert.Equal(expectedPhoneNumber, numberNode["PhoneNumber"]?.GetValue<string>());
        Assert.Equal(expectedCountryCode, numberNode["CountryCode"]?.GetValue<string>());
        Assert.Equal(expectedNationalNumber, numberNode["NationalNumber"]?.GetValue<string>());
    }

    [Fact]
    public void TryMigratePhoneNumberField_WhenPhoneNumberWasAlreadyMigrated_ReturnsFalse()
    {
        // Arrange
        var innerContentItem = new JsonObject
        {
            [OmnichannelConstants.ContentParts.PhoneNumberInfo] = new JsonObject
            {
                ["Number"] = new JsonObject
                {
                    ["PhoneNumber"] = "+14035550101",
                    ["CountryCode"] = "CA",
                    ["NationalNumber"] = "4035550101",
                },
            },
        };

        // Act
        var migrated = ContactMethodMigrations.TryMigratePhoneNumberField(innerContentItem, _phoneNumberService);

        // Assert
        Assert.False(migrated);
    }

    private static JsonObject CreateLegacyPhoneNumberContentItem(string legacyPhoneNumber) =>
        new()
        {
            ["ContentType"] = OmnichannelConstants.ContentTypes.PhoneNumber,
            [OmnichannelConstants.ContentParts.PhoneNumberInfo] = new JsonObject
            {
                ["Number"] = new JsonObject
                {
                    ["Text"] = legacyPhoneNumber,
                },
                ["Type"] = new JsonObject
                {
                    ["Text"] = "Home",
                },
            },
        };
}
