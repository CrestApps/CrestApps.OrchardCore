using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Core;

/// <summary>
/// The small shared helpers that replaced private copies: the part check, index column truncation and the phone
/// country codes.
/// </summary>
public sealed class SharedHelpersTests
{
    [Fact]
    public void HasPart_MatchesThePartDefinitionOrTheNamedPart()
    {
        // Arrange
        var definition = new ContentTypeDefinition(
            "Product",
            "Product",
            [
                new ContentTypePartDefinition("ProductPart", new ContentPartDefinition("ProductPart"), []),
                new ContentTypePartDefinition("Gallery", new ContentPartDefinition("BagPart"), []),
            ],
            []);

        // Act & Assert
        Assert.True(definition.HasPart("ProductPart"));
        Assert.True(definition.HasPart("BagPart"));
        Assert.True(definition.HasPart("Gallery"));
        Assert.False(definition.HasPart("ListPart"));
        Assert.False(((ContentTypeDefinition)null).HasPart("ProductPart"));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", null)]
    [InlineData("abc", "abc", "abc")]
    [InlineData("abcdef", "abcd", "abcd")]
    public void Truncate_KeepsNullsAndCutsToTheColumn(string value, string expected, string expectedOrNull)
    {
        Assert.Equal(expected, StringTruncation.Truncate(value, 4));
        Assert.Equal(expectedOrNull, StringTruncation.TruncateOrNull(value, 4));
    }

    [Theory]
    [InlineData(" 1 ", "+1")]
    [InlineData("+44", "+44")]
    [InlineData(" ", null)]
    public void TheCallingCode_HasALeadingPlus(string value, string expected)
        => Assert.Equal(expected, PhoneCountryCodes.NormalizeCallingCode(value));

    [Theory]
    [InlineData(" us ", "US")]
    [InlineData(null, null)]
    public void TheRegionCode_IsUpperCase(string value, string expected)
        => Assert.Equal(expected, PhoneCountryCodes.NormalizeRegionCode(value));
}
