using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class PhoneNumberSearchTermTests
{
    [Theory]
    [InlineData("702499", "702499", false)]
    [InlineData("(702) 499-3350", "7024993350", false)]
    [InlineData(" +1 (702) 499-3350 ", "+17024993350", true)]
    public void TryParse_WhenInputContainsDigits_NormalizesSearchValue(
        string input,
        string expectedValue,
        bool expectedIsE164)
    {
        // Act
        var parsed = PhoneNumberSearchTerm.TryParse(input, out var searchTerm);

        // Assert
        Assert.True(parsed);
        Assert.Equal(expectedValue, searchTerm.Value);
        Assert.Equal(expectedIsE164, searchTerm.IsE164);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+")]
    [InlineData("phone")]
    public void TryParse_WhenInputHasNoDigits_ReturnsFalse(string input)
    {
        // Act
        var parsed = PhoneNumberSearchTerm.TryParse(input, out _);

        // Assert
        Assert.False(parsed);
    }

    [Theory]
    [InlineData(PhoneNumberMatchType.Exact, "702499")]
    [InlineData(PhoneNumberMatchType.BeginsWith, "702499%")]
    [InlineData(PhoneNumberMatchType.EndsWith, "%702499")]
    [InlineData(PhoneNumberMatchType.Contains, "%702499%")]
    public void GetPattern_WhenMatchTypeIsValid_ReturnsExpectedPattern(
        PhoneNumberMatchType matchType,
        string expectedPattern)
    {
        // Arrange
        var parsed = PhoneNumberSearchTerm.TryParse("702499", out var searchTerm);

        // Act
        var pattern = searchTerm.GetPattern(matchType);

        // Assert
        Assert.True(parsed);
        Assert.Equal(expectedPattern, pattern);
    }

    [Fact]
    public void PhoneNumberFilters_WhenCreated_DefaultToContains()
    {
        // Arrange
        var batch = new OmnichannelActivityBatch();
        var filter = new BulkManageActivityFilter();

        // Assert
        Assert.Equal(PhoneNumberMatchType.Contains, batch.PhoneNumberMatchType);
        Assert.Equal(PhoneNumberMatchType.Contains, filter.PhoneNumberMatchType);
    }
}
