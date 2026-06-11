using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;
using CrestApps.OrchardCore.TimeZones.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.TimeZones;

public sealed class MappedTimeZoneSelectListProviderTests
{
    [Fact]
    public async Task GetTimeZoneSelectListItemsAsync_ShouldReturnMappedItemsOrderedByName()
    {
        // Arrange
        var catalog = new Mock<INamedCatalog<TimeZoneMap>>();
        catalog.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TimeZoneMap { Name = "India Standard Time", TimeZoneId = "Asia/Kolkata" },
                new TimeZoneMap { Name = "Central European Time", TimeZoneId = "Europe/Berlin" },
                new TimeZoneMap { Name = "Eastern Time (US & Canada)", TimeZoneId = "America/New_York" },
            ]);

        var provider = new MappedTimeZoneSelectListProvider(catalog.Object);

        // Act
        var items = await provider.GetTimeZoneSelectListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Collection(items,
            item => AssertSelectListItem(item, "Central European Time", "Europe/Berlin"),
            item => AssertSelectListItem(item, "Eastern Time (US & Canada)", "America/New_York"),
            item => AssertSelectListItem(item, "India Standard Time", "Asia/Kolkata"));
    }

    private static void AssertSelectListItem(KeyValuePair<string, string> item, string expectedText, string expectedValue)
    {
        Assert.Equal(expectedText, item.Value);
        Assert.Equal(expectedValue, item.Key);
    }
}
