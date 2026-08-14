using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Dialpad.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Dialpad;

public sealed class DialpadTelephonyProviderTimestampTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    [InlineData("ja-JP")]
    public void ReadDateTimeOffset_ParsesProviderTimestampIdenticallyRegardlessOfAmbientCulture(string cultureName)
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            using var document = JsonDocument.Parse("{\"date_started\":\"2024-03-05T14:30:00Z\"}");

            // Act
            var parsed = DialpadTelephonyProvider.ReadDateTimeOffset(document.RootElement, "date_started");

            // Assert
            Assert.NotNull(parsed);
            Assert.Equal(new DateTimeOffset(2024, 3, 5, 14, 30, 0, TimeSpan.Zero), parsed!.Value.ToUniversalTime());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ReadDateTimeOffset_NormalizesUnspecifiedTimestampToUtc()
    {
        // Arrange
        using var document = JsonDocument.Parse("{\"date_started\":\"2024-03-05 14:30:00\"}");

        // Act
        var parsed = DialpadTelephonyProvider.ReadDateTimeOffset(document.RootElement, "date_started");

        // Assert
        Assert.NotNull(parsed);
        Assert.Equal(TimeSpan.Zero, parsed!.Value.Offset);
        Assert.Equal(new DateTimeOffset(2024, 3, 5, 14, 30, 0, TimeSpan.Zero), parsed.Value);
    }

    [Fact]
    public void ReadDateTimeOffset_WhenPropertyMissing_ReturnsNull()
    {
        // Arrange
        using var document = JsonDocument.Parse("{}");

        // Act
        var parsed = DialpadTelephonyProvider.ReadDateTimeOffset(document.RootElement, "date_started");

        // Assert
        Assert.Null(parsed);
    }
}
