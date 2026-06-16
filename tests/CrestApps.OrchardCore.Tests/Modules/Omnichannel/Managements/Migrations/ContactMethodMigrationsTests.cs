using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Migrations;

public sealed class ContactMethodMigrationsTests
{
    private readonly DefaultPhoneNumberService _phoneNumberService = new();

    [Fact]
    public void TryMigratePhoneNumberField_WhenLegacyCanadianPhoneNumberExists_StoresPhoneFieldProperties()
    {
        // Arrange
        var innerContentItem = new JsonObject
        {
            ["ContentType"] = OmnichannelConstants.ContentTypes.PhoneNumber,
            [OmnichannelConstants.ContentParts.PhoneNumberInfo] = new JsonObject
            {
                ["Number"] = new JsonObject
                {
                    ["Text"] = "(403) 481-6330",
                },
                ["Type"] = new JsonObject
                {
                    ["Text"] = "Home",
                },
            },
        };

        // Act
        var migrated = ContactMethodMigrations.TryMigratePhoneNumberField(innerContentItem, _phoneNumberService);

        // Assert
        Assert.True(migrated);

        var phoneNumberInfoPart = Assert.IsType<JsonObject>(innerContentItem[OmnichannelConstants.ContentParts.PhoneNumberInfo]);
        var numberNode = Assert.IsType<JsonObject>(phoneNumberInfoPart["Number"]);

        Assert.Null(numberNode["Text"]);
        Assert.Equal("+14034816330", numberNode["PhoneNumber"]?.GetValue<string>());
        Assert.Equal("CA", numberNode["CountryCode"]?.GetValue<string>());
        Assert.Equal("4034816330", numberNode["NationalNumber"]?.GetValue<string>());
    }

    [Fact]
    public void TryMigratePhoneNumberField_WhenLegacyPossibleCanadianPhoneNumberExists_StoresPhoneFieldProperties()
    {
        // Arrange
        var innerContentItem = new JsonObject
        {
            ["ContentType"] = OmnichannelConstants.ContentTypes.PhoneNumber,
            [OmnichannelConstants.ContentParts.PhoneNumberInfo] = new JsonObject
            {
                ["Number"] = new JsonObject
                {
                    ["Text"] = "(778) 552-8744",
                },
                ["Type"] = new JsonObject
                {
                    ["Text"] = "Home",
                },
            },
        };

        // Act
        var migrated = ContactMethodMigrations.TryMigratePhoneNumberField(innerContentItem, _phoneNumberService);

        // Assert
        Assert.True(migrated);

        var phoneNumberInfoPart = Assert.IsType<JsonObject>(innerContentItem[OmnichannelConstants.ContentParts.PhoneNumberInfo]);
        var numberNode = Assert.IsType<JsonObject>(phoneNumberInfoPart["Number"]);

        Assert.Null(numberNode["Text"]);
        Assert.Equal("+17785528744", numberNode["PhoneNumber"]?.GetValue<string>());
        Assert.Equal("CA", numberNode["CountryCode"]?.GetValue<string>());
        Assert.Equal("7785528744", numberNode["NationalNumber"]?.GetValue<string>());
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
                    ["PhoneNumber"] = "+14034816330",
                    ["CountryCode"] = "CA",
                    ["NationalNumber"] = "4034816330",
                },
            },
        };

        // Act
        var migrated = ContactMethodMigrations.TryMigratePhoneNumberField(innerContentItem, _phoneNumberService);

        // Assert
        Assert.False(migrated);
    }
}
