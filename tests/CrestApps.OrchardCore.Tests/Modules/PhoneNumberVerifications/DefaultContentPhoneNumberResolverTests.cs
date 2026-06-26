using System.Text.Json.Nodes;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumberVerifications;

public sealed class DefaultContentPhoneNumberResolverTests
{
    [Fact]
    public async Task GetPhoneNumberAsync_WhenFieldNameContainsPhoneWithText_ReturnsValue()
    {
        // Arrange
        var resolver = new DefaultContentPhoneNumberResolver();
        var contentItem = CreateContentItem(new JsonObject
        {
            ["ContactPart"] = new JsonObject
            {
                ["MobilePhone"] = new JsonObject
                {
                    ["Text"] = "+17024993350",
                },
            },
        });

        // Act
        var phoneNumber = await resolver.GetPhoneNumberAsync(contentItem);

        // Assert
        Assert.Equal("+17024993350", phoneNumber);
    }

    [Fact]
    public async Task GetPhoneNumberAsync_WhenFieldUsesValueProperty_ReturnsValue()
    {
        // Arrange
        var resolver = new DefaultContentPhoneNumberResolver();
        var contentItem = CreateContentItem(new JsonObject
        {
            ["ContactPart"] = new JsonObject
            {
                ["PrimaryPhoneNumber"] = new JsonObject
                {
                    ["Value"] = "+447911123456",
                },
            },
        });

        // Act
        var phoneNumber = await resolver.GetPhoneNumberAsync(contentItem);

        // Assert
        Assert.Equal("+447911123456", phoneNumber);
    }

    [Fact]
    public async Task GetPhoneNumberAsync_WhenScalarPhoneField_ReturnsValue()
    {
        // Arrange
        var resolver = new DefaultContentPhoneNumberResolver();
        var contentItem = CreateContentItem(new JsonObject
        {
            ["ContactPart"] = new JsonObject
            {
                ["HomePhone"] = "+13125550123",
            },
        });

        // Act
        var phoneNumber = await resolver.GetPhoneNumberAsync(contentItem);

        // Assert
        Assert.Equal("+13125550123", phoneNumber);
    }

    [Fact]
    public async Task GetPhoneNumberAsync_WhenNoPhoneField_ReturnsNull()
    {
        // Arrange
        var resolver = new DefaultContentPhoneNumberResolver();
        var contentItem = CreateContentItem(new JsonObject
        {
            ["ContactPart"] = new JsonObject
            {
                ["EmailAddress"] = new JsonObject
                {
                    ["Text"] = "person@example.com",
                },
            },
        });

        // Act
        var phoneNumber = await resolver.GetPhoneNumberAsync(contentItem);

        // Assert
        Assert.Null(phoneNumber);
    }

    private static ContentItem CreateContentItem(JsonObject content)
    {
        var contentItem = new ContentItem
        {
            ContentType = "Contact",
        };

        var target = (JsonObject)contentItem.Content;

        foreach (var property in content)
        {
            target[property.Key] = property.Value?.DeepClone();
        }

        return contentItem;
    }
}
