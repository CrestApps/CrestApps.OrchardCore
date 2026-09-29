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

    [Theory]
    [InlineData("5555550101", "+15555550101")]
    [InlineData("15555550101", "+15555550101")]
    [InlineData("+15555550101", "+15555550101")]
    [InlineData("445555550101", "+445555550101")]
    public void GetExactE164Candidates_WhenAnExactSearchCanMeanAnE164Number_IncludesIt(string input, string expected)
    {
        // Arrange
        // An exact national search compared only the national columns, so a number whose national column held
        // something else (the country code, or nothing at all) was never found. The E.164 shape of the entry is
        // what reaches those records.
        Assert.True(PhoneNumberSearchTerm.TryParse(input, out var searchTerm));

        // Act
        var candidates = searchTerm.GetExactE164Candidates();

        // Assert
        Assert.Equal([expected], candidates);
    }

    [Theory]
    [InlineData("555")]
    [InlineData("555550101")]
    [InlineData("05555550101")]
    public void GetExactE164Candidates_WhenTheEntryCannotBeAWholeNumber_ReturnsNone(string input)
    {
        // Arrange
        // A partial number, or one starting with a trunk zero, is not an E.164 number in any country, so it must not
        // be turned into one and matched against a stranger's number.
        Assert.True(PhoneNumberSearchTerm.TryParse(input, out var searchTerm));

        // Act
        var candidates = searchTerm.GetExactE164Candidates();

        // Assert
        Assert.Empty(candidates);
    }

    [Theory]
    [InlineData("5555550101", new[] { "15555550101" })]
    [InlineData("15555550101", new[] { "5555550101" })]
    [InlineData("+15555550101", new[] { "15555550101", "5555550101" })]
    [InlineData("+445555550101", new[] { "445555550101" })]
    [InlineData("555", new string[0])]
    public void GetExactUncanonicalNationalCandidates_WhenTheNumberWasImportedWithoutACountry_ListsTheShapesItMayHaveBeenStoredIn(string input, string[] expected)
    {
        // Arrange
        // A raw import without a country leaves the E.164 column empty and whatever digits arrived in the national
        // column. The exact search looks for the number in those shapes, but only on such records.
        Assert.True(PhoneNumberSearchTerm.TryParse(input, out var searchTerm));

        // Act
        var candidates = searchTerm.GetExactUncanonicalNationalCandidates();

        // Assert
        Assert.Equal(expected, candidates);
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
