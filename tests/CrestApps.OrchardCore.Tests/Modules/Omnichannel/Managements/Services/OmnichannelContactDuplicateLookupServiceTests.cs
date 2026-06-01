using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelContactDuplicateLookupServiceTests
{
    [Fact]
    public void AddLegacyMatches_ShouldMatchNormalizedInputAgainstLegacyPhoneValues()
    {
        // Arrange
        var existingPhoneNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        OmnichannelContactDuplicateLookupService.AddLegacyMatches(
            existingPhoneNumbers,
            [
            new OmnichannelContactIndex
            {
                PrimaryCellPhoneNumber = "(555) 111-2222",
            },
            ],
            ["5551112222"],
            static index => index.PrimaryCellPhoneNumber);

        // Assert
        Assert.Single(existingPhoneNumbers);
        Assert.Contains("5551112222", existingPhoneNumbers);
    }

    [Fact]
    public void NormalizePhoneNumber_ShouldStripNonDigitCharacters()
    {
        Assert.Equal("5551112222", OmnichannelContactDuplicateLookupService.NormalizePhoneNumber("(555) 111-2222"));
        Assert.Equal("5551112222", OmnichannelContactDuplicateLookupService.NormalizePhoneNumber("555.111.2222"));
        Assert.Equal("5551112222", OmnichannelContactDuplicateLookupService.NormalizePhoneNumber("555-111-2222"));
        Assert.Equal("", OmnichannelContactDuplicateLookupService.NormalizePhoneNumber(""));
        Assert.Equal("", OmnichannelContactDuplicateLookupService.NormalizePhoneNumber(null));
    }
}
