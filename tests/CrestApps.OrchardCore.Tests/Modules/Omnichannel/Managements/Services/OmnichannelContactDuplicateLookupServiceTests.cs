using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Moq;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelContactDuplicateLookupServiceTests
{
    [Fact]
    public void AddLegacyMatches_ShouldMatchInputAgainstLegacyPhoneValues()
    {
        // Arrange
        var existingPhoneNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        OmnichannelContactDuplicateLookupService.AddLegacyMatches(
            existingPhoneNumbers,
            [
            new OmnichannelContactIndex
            {
                PrimaryCellPhoneNumber = "+15551112222",
            },
            ],
            ["+15551112222"],
            static index => index.PrimaryCellPhoneNumber);

        // Assert
        Assert.Single(existingPhoneNumbers);
        Assert.Contains("+15551112222", existingPhoneNumbers);
    }

    [Fact]
    public void NormalizePhoneNumber_WhenValidE164_ShouldReturnE164()
    {
        // Arrange
        var phoneNumberService = new DefaultPhoneNumberService();
        var session = Mock.Of<ISession>();
        var service = new OmnichannelContactDuplicateLookupService(session, phoneNumberService);

        // Act & Assert
        Assert.Equal("+17024993350", service.NormalizePhoneNumber("+17024993350"));
        Assert.Equal("+17024993350", service.NormalizePhoneNumber("+1 (702) 499-3350"));

        // Numbers without + fallback to digits-only normalization.
        Assert.Equal("7024993350", service.NormalizePhoneNumber("702-499-3350"));
        Assert.Equal("7024993350", service.NormalizePhoneNumber("(702) 499-3350"));
    }

    [Fact]
    public void NormalizePhoneNumber_WhenEmptyOrNull_ShouldReturnEmpty()
    {
        // Arrange
        var phoneNumberService = new DefaultPhoneNumberService();
        var session = Mock.Of<ISession>();
        var service = new OmnichannelContactDuplicateLookupService(session, phoneNumberService);

        // Act & Assert
        Assert.Equal(string.Empty, service.NormalizePhoneNumber(""));
        Assert.Equal(string.Empty, service.NormalizePhoneNumber(null));
    }
}
